import { useCallback, useEffect, useRef, useState } from "react";
import { getAuthSession } from "../../auth/utils/authStorage";
import { deleteCheckoutDraft, readCheckoutDraft, saveCheckoutDraft } from "../api/checkoutDraftApi";
import { EMPTY_DRAFT } from "../utils/checkoutDraft";
import type { CheckoutDraft } from "../types/checkout.types";
import { getErrorMessage } from "../../../shared/utils/errorHandling";

export function useCheckoutDraft(cartId: string | undefined) {
  const [draft, setDraft] = useState<CheckoutDraft>({ ...EMPTY_DRAFT });
  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);
  const [reload, setReload] = useState(0);
  const version = useRef<string | null>(null);
  const saved = useRef(JSON.stringify(EMPTY_DRAFT));
  const queue = useRef<Promise<void>>(Promise.resolve());
  const generation = useRef(0);
  const paused = useRef(false);
  const blocked = useRef(false);
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  useEffect(() => {
    const current = ++generation.current;
    const sessionId = getAuthSession()?.sessionId;
    let cancelled = false;
    setReady(false);
    setSaving(false);
    setError("");
    paused.current = false;
    blocked.current = false;
    if (!cartId) return;
    void queue.current.then(() => {
      if (cancelled || sessionId !== getAuthSession()?.sessionId) throw new DOMException("Session changed", "AbortError");
      return readCheckoutDraft(cartId);
    }).then(result => {
      if (cancelled || current !== generation.current || sessionId !== getAuthSession()?.sessionId) return;
      const fields = result.fields ?? { ...EMPTY_DRAFT };
      version.current = result.version;
      saved.current = JSON.stringify(fields);
      setDraft(fields);
      setReady(true);
    }).catch(caught => {
      if (!cancelled) setError(getErrorMessage(caught, "A piszkozat betöltése nem sikerült."));
    });
    return () => { cancelled = true; generation.current = current + 1; clearTimeout(timer.current); };
  }, [cartId, reload]);

  const save = useCallback(() => {
    if (!cartId || !ready || paused.current || blocked.current) return;
    const json = JSON.stringify(draft);
    if (json === saved.current) return;
    const current = generation.current;
    const sessionId = getAuthSession()?.sessionId;
    const isCurrent = () => current === generation.current && sessionId === getAuthSession()?.sessionId;
    const work = queue.current.then(async () => {
      if (!isCurrent() || paused.current || blocked.current || json === saved.current) return;
      setSaving(true);
      try {
        const result = await saveCheckoutDraft(cartId, version.current, draft);
        if (!isCurrent()) return;
        version.current = result.version;
        saved.current = json;
        setError("");
      } catch (caught) {
        if (isCurrent()) {
          blocked.current = true;
          setError(getErrorMessage(caught, "A piszkozat mentése nem sikerült."));
        }
      } finally { if (isCurrent()) setSaving(false); }
    });
    queue.current = work.catch(() => undefined);
  }, [cartId, draft, ready]);

  useEffect(() => {
    timer.current = setTimeout(save, 800);
    return () => clearTimeout(timer.current);
  }, [save]);

  useEffect(() => {
    const beforeUnload = (event: BeforeUnloadEvent) => {
      if (ready && !paused.current && JSON.stringify(draft) !== saved.current) event.preventDefault();
    };
    window.addEventListener("beforeunload", beforeUnload);
    return () => window.removeEventListener("beforeunload", beforeUnload);
  }, [draft, ready]);

  const pause = useCallback(async () => {
    paused.current = true;
    clearTimeout(timer.current);
    await queue.current;
  }, []);

  const discard = useCallback(async () => {
    if (!cartId || !ready) return false;
    await pause();
    try {
      await deleteCheckoutDraft(cartId, version.current);
      version.current = null;
      saved.current = JSON.stringify(EMPTY_DRAFT);
      setDraft({ ...EMPTY_DRAFT });
      setError("");
      blocked.current = false;
      return true;
    } catch (caught) { setError(getErrorMessage(caught, "A piszkozat törlése nem sikerült.")); return false; }
    finally { paused.current = false; }
  }, [cartId, pause, ready]);

  return { draft, setDraft, ready, error, saving, discard, pause,
    resume: () => { paused.current = false; },
    retry: () => { blocked.current = false; save(); },
    load: () => setReload(value => value + 1),
  };
}
