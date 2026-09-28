import { useSyncExternalStore } from "react";
import { AUTH_CHANGED_EVENT, AUTH_STORAGE_KEY, parseAuthSession } from "../utils/authStorage";

function subscribe(onChange: () => void) {
  window.addEventListener(AUTH_CHANGED_EVENT, onChange);
  return () => window.removeEventListener(AUTH_CHANGED_EVENT, onChange);
}

export function useAuthSession() {
  const stored = useSyncExternalStore(subscribe, () => localStorage.getItem(AUTH_STORAGE_KEY), () => null);
  return parseAuthSession(stored);
}
