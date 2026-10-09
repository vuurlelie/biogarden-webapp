import { useCallback, useEffect, useState } from "react";
import { useAnalytics } from "../context/AnalyticsContextCore";
import { OPEN_CONSENT_PREFERENCES_EVENT } from "../utils/analyticsEvents";

export default function ConsentBanner() {
  const {
    analyticsEnabled,
    closePreferences,
    hasConsentChoice,
    openPreferences,
    preferencesOpen,
    saveConsent,
  } = useAnalytics();
  const [analyticsChecked, setAnalyticsChecked] = useState(analyticsEnabled);

  const openSettings = useCallback(
    () => {
      setAnalyticsChecked(analyticsEnabled);
      openPreferences();
    },
    [analyticsEnabled, openPreferences]
  );

  useEffect(
    () => {
      const open = () => openSettings();
      window.addEventListener(OPEN_CONSENT_PREFERENCES_EVENT, open);
      return () => window.removeEventListener(OPEN_CONSENT_PREFERENCES_EVENT, open);
    },
    [openSettings]
  );

  if (hasConsentChoice && !preferencesOpen)
    return null;

  return (
    <div className="fixed inset-x-0 bottom-0 z-40 px-4 pb-4 sm:px-6" role="region" aria-label="Adatvédelmi beállítások">
      <div className="mx-auto max-w-4xl rounded-sm border border-site-border bg-white p-4 shadow-2xl sm:p-5">
        <div className="grid gap-4 md:grid-cols-[minmax(0,1fr)_auto] md:items-end">
          <div>
            <h2 className="text-lg font-bold text-site-heading">Sütik és adatvédelmi beállítások</h2>
            <p className="mt-2 text-sm leading-6 text-site-text">
              A weboldal működéséhez szükséges adatokat mindig kezeljük. Az anonim látogatottsági
              analitikát csak akkor kapcsoljuk be, ha ehhez hozzájárulsz.
            </p>

            {preferencesOpen && (
              <label className="mt-4 flex items-start gap-3 rounded-sm border border-site-border bg-site-surface-soft p-3 text-sm text-site-text">
                <input
                  type="checkbox"
                  checked={analyticsChecked}
                  onChange={(event) => setAnalyticsChecked(event.target.checked)}
                  className="mt-1 h-4 w-4 accent-site-green"
                />
                <span>
                  <span className="block font-semibold text-site-heading">Anonim látogatottsági analitika</span>
                  <span className="mt-1 block">
                    Segít megérteni, mely oldalak és rendelési lépések működnek jól. Nem küldünk nevet,
                    email címet, telefonszámot, pontos címet vagy rendelésazonosítót.
                  </span>
                </span>
              </label>
            )}
          </div>

          <div className="flex flex-col gap-2 sm:flex-row md:justify-end">
            {!preferencesOpen && (
              <button
                type="button"
                onClick={openSettings}
                className="rounded-sm border border-site-border px-4 py-2.5 text-sm font-semibold text-site-heading hover:bg-site-surface-soft"
              >
                Beállítások
              </button>
            )}
            <button
              type="button"
              onClick={() => saveConsent({ necessary: true, analytics: preferencesOpen ? analyticsChecked : false })}
              className="rounded-sm border border-site-green-dark px-4 py-2.5 text-sm font-semibold text-site-green-dark hover:bg-site-surface-soft"
            >
              {preferencesOpen ? "Mentés" : "Csak szükséges"}
            </button>
            {!preferencesOpen && (
              <button
                type="button"
                onClick={() => saveConsent({ necessary: true, analytics: true })}
                className="rounded-sm bg-site-green px-4 py-2.5 text-sm font-semibold text-white hover:bg-site-green-hover"
              >
                Analitika engedélyezése
              </button>
            )}
            {preferencesOpen && hasConsentChoice && (
              <button
                type="button"
                onClick={closePreferences}
                className="rounded-sm border border-site-border px-4 py-2.5 text-sm font-semibold text-site-heading hover:bg-site-surface-soft"
              >
                Bezárás
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
