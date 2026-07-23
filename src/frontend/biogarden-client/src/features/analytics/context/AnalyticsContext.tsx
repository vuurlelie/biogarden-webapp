import {
  useCallback,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import type {
  AnalyticsEvent,
  AnalyticsEventName,
  AnalyticsPayload,
  ConsentPreferences,
  StoredConsentPreferences,
} from "../types/analytics.types";
import { loadStoredConsent, saveStoredConsent } from "../utils/consentStorage";
import { sanitizeAnalyticsPayload } from "../utils/eventValidation";
import { ANALYTICS_EVENT_DISPATCHED } from "../utils/analyticsEvents";
import { AnalyticsContext, type AnalyticsContextValue } from "./AnalyticsContextCore";

export function AnalyticsProvider({ children }: { children: ReactNode }) {
  const [consent, setConsent] = useState<StoredConsentPreferences | null>(() => loadStoredConsent());
  const [preferencesOpen, setPreferencesOpen] = useState(false);

  const analyticsEnabled = consent?.analytics === true;

  const saveConsent = useCallback((preferences: ConsentPreferences) => {
    const stored = saveStoredConsent(preferences);
    setConsent(stored);
    setPreferencesOpen(false);
  }, []);

  const track = useCallback(
    (name: AnalyticsEventName, payload: AnalyticsPayload = {}) => {
      if (!analyticsEnabled) return;

      const event: AnalyticsEvent = {
        name,
        payload: sanitizeAnalyticsPayload(name, payload),
        occurredAtUtc: new Date().toISOString(),
      };

      window.dispatchEvent(new CustomEvent(ANALYTICS_EVENT_DISPATCHED, { detail: event }));
    },
    [analyticsEnabled],
  );

  const value = useMemo<AnalyticsContextValue>(
    () => ({
      consent,
      analyticsEnabled,
      preferencesOpen,
      hasConsentChoice: consent !== null,
      openPreferences: () => setPreferencesOpen(true),
      closePreferences: () => setPreferencesOpen(false),
      saveConsent,
      track,
    }),
    [analyticsEnabled, consent, preferencesOpen, saveConsent, track],
  );

  return <AnalyticsContext.Provider value={value}>{children}</AnalyticsContext.Provider>;
}
