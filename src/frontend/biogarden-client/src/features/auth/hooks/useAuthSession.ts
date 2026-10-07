import { useSyncExternalStore } from "react";
import { AUTH_CHANGED_EVENT, getAuthSession } from "../utils/authStorage";

function subscribe(onChange: () => void) {
  window.addEventListener(AUTH_CHANGED_EVENT, onChange);
  window.addEventListener("biogarden:session-updated", onChange);
  return () => { window.removeEventListener(AUTH_CHANGED_EVENT, onChange); window.removeEventListener("biogarden:session-updated", onChange); };
}

export function useAuthSession() {
  return useSyncExternalStore(subscribe, getAuthSession, () => null);
}
