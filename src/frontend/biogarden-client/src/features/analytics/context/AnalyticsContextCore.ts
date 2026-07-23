import { createContext, useContext } from "react";
import type {
  AnalyticsEventName,
  AnalyticsPayload,
  ConsentPreferences,
  StoredConsentPreferences,
} from "../types/analytics.types";

export type AnalyticsContextValue = {
  consent: StoredConsentPreferences | null;
  analyticsEnabled: boolean;
  preferencesOpen: boolean;
  hasConsentChoice: boolean;
  openPreferences: () => void;
  closePreferences: () => void;
  saveConsent: (preferences: ConsentPreferences) => void;
  track: (name: AnalyticsEventName, payload?: AnalyticsPayload) => void;
};

export const AnalyticsContext = createContext<AnalyticsContextValue | undefined>(undefined);

export function useAnalytics() {
  const value = useContext(AnalyticsContext);
  if (!value) {
    throw new Error("useAnalytics must be used within AnalyticsProvider.");
  }

  return value;
}
