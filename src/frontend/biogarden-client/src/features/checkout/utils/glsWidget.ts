// Official adapter contract: https://map.gls-hungary.com/widget/example/
export const GLS_WIDGET_URL = "https://map.gls-hungary.com/widget/gls-dpm.js";
export const GLS_WIDGET_TAG = "gls-dpm";
export const GLS_WIDGET_TIMEOUT_MS = 20_000;
let pending: Promise<void> | undefined;

export function loadGlsWidget(): Promise<void> {
  if (customElements.get(GLS_WIDGET_TAG)) return Promise.resolve();
  if (pending) return pending;
  pending = new Promise<void>((resolve, reject) => {
    const script = document.createElement("script");
    script.src = GLS_WIDGET_URL;
    script.async = true;
    script.referrerPolicy = "no-referrer";
    const timer = window.setTimeout(fail, GLS_WIDGET_TIMEOUT_MS);
    function fail() {
      window.clearTimeout(timer);
      script.remove();
      reject(new Error("A GLS térkép nem tölthető be. Próbáld újra, vagy válassz másik átvételi módot."));
    }
    script.onerror = fail;
    void customElements.whenDefined(GLS_WIDGET_TAG).then(() => { window.clearTimeout(timer); resolve(); });
    document.head.append(script);
  }).catch(error => { pending = undefined; throw error; });
  return pending;
}
