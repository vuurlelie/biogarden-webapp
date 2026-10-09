import { useEffect, useRef, useState } from "react";
import { resolveGlsPoint, type GlsDeliveryPoint } from "../api/glsApi";
import { FULFILLMENT, FULFILLMENT_LABELS, type GlsPointMethod } from "../constants/fulfillment";
import { GLS_WIDGET_TAG, loadGlsWidget } from "../utils/glsWidget";
import { getErrorMessage } from "../../../shared/utils/errorHandling";

type Props = {
  method: GlsPointMethod;
  point: GlsDeliveryPoint | null;
  loading: boolean;
  error?: string;
  onRetry: () => void;
  onSelect: (point: GlsDeliveryPoint) => void;
};

export function GlsPointPicker({ method, point, loading, error, onRetry, onSelect }: Props) {
  const [open, setOpen] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const [mapLoading, setMapLoading] = useState(false);
  const [mapError, setMapError] = useState("");
  const [checking, setChecking] = useState(false);
  const dialog = useRef<HTMLDialogElement>(null);
  const host = useRef<HTMLDivElement>(null);
  const trigger = useRef<HTMLButtonElement>(null);
  const onSelectRef = useRef(onSelect);
  useEffect(
    () => { onSelectRef.current = onSelect; },
    [onSelect]
  );

  useEffect(
    () => {
      if (!open)
        return;
      const element = dialog.current!;
      const returnFocus = trigger.current;
      element.showModal();
      const overflow = document.body.style.overflow;
      document.body.style.overflow = "hidden";
      return () => { element.close(); document.body.style.overflow = overflow; returnFocus?.focus(); };
    },
    [open]
  );

  useEffect(
    () => {
      if (!open)
        return;
      let cancelled = false;
      let request: AbortController | undefined;
      let widget: HTMLElement | undefined;
      const change = (event: Event) => {
        const detail: unknown = (event as CustomEvent<unknown>).detail;
        request?.abort();
        if (!detail || typeof detail !== "object" || !("id" in detail) || typeof detail.id !== "string" || !detail.id || detail.id.length > 100) {
          setChecking(false);
          setMapError("Nem érvényes GLS pont. Válassz új pontot a térképen.");
          return;
        }
        const controller = new AbortController();
        request = controller;
        setChecking(true);
        setMapError("");
        void resolveGlsPoint(detail.id, method, controller.signal).then(resolved => {
          if (cancelled || controller.signal.aborted)
            return;
          onSelectRef.current(resolved);
          setOpen(false);
        }).catch(caught => {
          if (!cancelled && !controller.signal.aborted)
            setMapError(getErrorMessage(caught, "Az átvételi pont ellenőrzése nem sikerült."));
        }).finally(() => {
          if (!cancelled && !controller.signal.aborted)
            setChecking(false);
        });
      };
      void loadGlsWidget().then(() => {
        if (cancelled)
          return;
        widget = document.createElement(GLS_WIDGET_TAG);
        widget.setAttribute("country", "hu");
        widget.setAttribute("language", "hu");
        widget.setAttribute("filter-type", method === FULFILLMENT.ParcelLocker ? "parcel-locker" : "parcel-shop");
        widget.setAttribute("filter-services", "delivery");
        widget.setAttribute("ui-filters", "opening-hours,features");
        widget.addEventListener("change", change);
        host.current?.replaceChildren(widget);
        setMapLoading(false);
      }).catch(caught => {
        if (!cancelled) {
          setMapLoading(false);
          setMapError(getErrorMessage(caught, "A GLS térkép nem tölthető be."));
        }
      });
      return () => { cancelled = true; request?.abort(); widget?.removeEventListener("change", change); widget?.remove(); };
    },
    [open, method, attempt]
  );

  return <section className="rounded-sm border border-site-border bg-white p-6 shadow-md" aria-label="GLS átvételi pont">
    <h2 className="text-xl font-bold">{FULFILLMENT_LABELS[method]} kiválasztása</h2>
    <p className="my-3 text-sm">A térképen válassz átvételi pontot. A megnyitáskor a GLS térképszolgáltatása betöltődik.</p>
    {point && <div className="my-3 rounded-sm bg-site-surface-soft p-3" role="status">
      <strong>{point.name}</strong><p>{point.postalCode} {point.city}, {point.address}</p>
      <p className="text-sm">{point.acceptsCard && !point.acceptsCash ? "Utánvét esetén itt bankkártyával fizethetsz." : !point.acceptsCard && !point.acceptsCash ? "Ezen a ponton utánvétes fizetés nem érhető el." : ""}</p>
    </div>}
    {loading && <p role="status">Átvételi pont ellenőrzése...</p>}
    {error && <p id="gls-selection-error" role="alert" className="my-2 text-site-danger">{error}</p>}
    <button ref={trigger} type="button" aria-invalid={Boolean(error)} aria-describedby={error ? "gls-selection-error" : undefined}
      onClick={() => { setMapLoading(true); setMapError(""); setChecking(false); setOpen(true); }} className="rounded-sm border border-site-green px-4 py-3 font-semibold">
      {point ? "GLS átvételi pont módosítása" : "GLS térkép megnyitása"}
    </button>
    {error && <button type="button" onClick={onRetry} className="ml-3 underline">Átvételi pont újraellenőrzése</button>}
    {open && <dialog ref={dialog} aria-labelledby="gls-picker-title" onCancel={() => setOpen(false)}
      className="m-auto max-h-[95dvh] w-[min(70rem,96vw)] max-w-none overflow-auto rounded-sm bg-white p-3 backdrop:bg-black/50 sm:p-5">
      <div className="mb-3 flex items-center justify-between gap-3">
        <h2 id="gls-picker-title" className="text-xl font-bold">{FULFILLMENT_LABELS[method]} térkép</h2>
        <button type="button" autoFocus onClick={() => setOpen(false)} aria-label="GLS térkép bezárása" className="rounded-sm border px-3 py-2">Bezárás</button>
      </div>
      <p role="status">{mapLoading ? "GLS térkép betöltése..." : checking ? "Kiválasztott pont ellenőrzése..." : "Keress településre vagy irányítószámra, majd válassz átvételi pontot."}</p>
      {mapError && <p role="alert" className="my-2 text-site-danger">{mapError}</p>}
      <div ref={host} className="mt-2 h-[65dvh] min-h-80" />
      <button type="button" onClick={() => { setMapLoading(true); setMapError(""); setChecking(false); setAttempt(value => value + 1); }} className="mt-3 underline">Térkép újratöltése</button>
    </dialog>}
  </section>;
}
