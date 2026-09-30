import { useEffect, useMemo, useState } from "react";
import { resolveGlsPoint, type GlsDeliveryPoint } from "../api/glsApi";
import { isGlsMethod, type FulfillmentMethod } from "../constants/fulfillment";
import { getErrorMessage } from "../../../shared/utils/errorHandling";

export function useGlsDeliveryPoint(id: string | null, method: FulfillmentMethod) {
  const [attempt, setAttempt] = useState(0);
  const key = useMemo(() => ({ id, method, attempt }), [id, method, attempt]);
  const [result, setResult] = useState<{ key: typeof key; point?: GlsDeliveryPoint; error?: string } | null>(null);
  useEffect(() => {
    if (!id || !isGlsMethod(method)) return;
    const controller = new AbortController();
    void resolveGlsPoint(id, method, controller.signal).then(point => {
      if (!controller.signal.aborted) setResult({ key, point });
    }).catch(error => {
      if (!controller.signal.aborted) setResult({ key, error: getErrorMessage(error, "Az átvételi pont ellenőrzése nem sikerült.") });
    });
    return () => controller.abort();
  }, [id, method, key]);
  const current = result?.key === key ? result : null;
  return { point: current?.point ?? null, error: current?.error,
    loading: Boolean(id && isGlsMethod(method) && !current), retry: () => { setResult(null); setAttempt(value => value + 1); } };
}
