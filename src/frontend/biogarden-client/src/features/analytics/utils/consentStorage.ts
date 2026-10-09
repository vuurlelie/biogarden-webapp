import type { ConsentPreferences, StoredConsentPreferences } from "../types/analytics.types";

export const CONSENT_STORAGE_KEY = "biogarden_privacy_consent";
export const CONSENT_VERSION = 1;

export function createStoredConsent(preferences: ConsentPreferences): StoredConsentPreferences {
  return {
    ...preferences,
    necessary: true,
    version: CONSENT_VERSION,
    updatedAtUtc: new Date().toISOString(),
  };
}

export function loadStoredConsent(): StoredConsentPreferences | null {
  try {
    const raw = window.localStorage.getItem(CONSENT_STORAGE_KEY);
    if (!raw)
      return null;

    const parsed = JSON.parse(raw) as Partial<StoredConsentPreferences>;
    if (parsed.version !== CONSENT_VERSION || parsed.necessary !== true)
      return null;

    return {
      necessary: true,
      analytics: parsed.analytics === true,
      version: CONSENT_VERSION,
      updatedAtUtc: typeof parsed.updatedAtUtc === "string" ? parsed.updatedAtUtc : new Date().toISOString(),
    };
  } catch {
    return null;
  }
}

export function saveStoredConsent(preferences: ConsentPreferences): StoredConsentPreferences {
  const stored = createStoredConsent(preferences);
  window.localStorage.setItem(CONSENT_STORAGE_KEY, JSON.stringify(stored));
  return stored;
}
