import type { AuthResponse } from "../types/auth.types";

export const AUTH_STORAGE_KEY = "biogarden_auth";
export const AUTH_CHANGED_EVENT = "biogarden:auth-changed";

export function saveAuthSession(auth: AuthResponse): void {
  localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(auth));
  window.dispatchEvent(new Event(AUTH_CHANGED_EVENT));
}

export function clearAuthSession(): void {
  if (localStorage.getItem(AUTH_STORAGE_KEY) === null) return;
  localStorage.removeItem(AUTH_STORAGE_KEY);
  window.dispatchEvent(new Event(AUTH_CHANGED_EVENT));
}

function tokenExpiresAt(token: string): number {
  try {
    const payloadPart = token.split(".")[1];
    if (!payloadPart) return 0;

    const normalizedPayload = payloadPart
      .replace(/-/g, "+")
      .replace(/_/g, "/")
      .padEnd(Math.ceil(payloadPart.length / 4) * 4, "=");
    const payload = JSON.parse(atob(normalizedPayload)) as { exp?: number };

    return typeof payload.exp === "number" && Number.isFinite(payload.exp) ? payload.exp * 1000 : 0;
  } catch {
    return 0;
  }
}

export function parseAuthSession(storedAuth: string | null): AuthResponse | null {
  if (!storedAuth) return null;

  try {
    const auth = JSON.parse(storedAuth) as Partial<AuthResponse>;

    if (
      typeof auth.userId !== "string" ||
      typeof auth.email !== "string" ||
      typeof auth.token !== "string" ||
      !Array.isArray(auth.roles) ||
      !auth.roles.every((role) => typeof role === "string") ||
      tokenExpiresAt(auth.token) <= Date.now()
    ) {
      return null;
    }

    return auth as AuthResponse;
  } catch {
    return null;
  }
}

export function getAuthSession(): AuthResponse | null {
  const stored = localStorage.getItem(AUTH_STORAGE_KEY);
  const auth = parseAuthSession(stored);
  if (stored && !auth) clearAuthSession();
  return auth;
}

// Start once at the application boundary, rather than installing one timer per component.
export function startAuthSessionSync(): () => void {
  let previous = localStorage.getItem(AUTH_STORAGE_KEY);
  let timer: ReturnType<typeof setTimeout> | undefined;
  const synchronize = () => {
    const stored = localStorage.getItem(AUTH_STORAGE_KEY);
    if (previous && stored !== previous) {
      sessionStorage.removeItem("biogarden_checkout_draft");
      sessionStorage.removeItem("biogarden_checkout_idempotency_key");
    }
    previous = stored;
    clearTimeout(timer);
    const auth = parseAuthSession(stored);
    if (stored && !auth) {
      clearAuthSession();
    } else if (auth) {
      timer = setTimeout(synchronize, Math.min(tokenExpiresAt(auth.token) - Date.now(), 2_147_483_647));
    }
  };
  const onStorage = (event: StorageEvent) => {
    if (event.storageArea === localStorage && (event.key === AUTH_STORAGE_KEY || event.key === null)) {
      window.dispatchEvent(new Event(AUTH_CHANGED_EVENT));
    }
  };
  window.addEventListener("storage", onStorage);
  window.addEventListener("focus", synchronize);
  window.addEventListener(AUTH_CHANGED_EVENT, synchronize);
  synchronize();
  return () => {
    clearTimeout(timer);
    window.removeEventListener("storage", onStorage);
    window.removeEventListener("focus", synchronize);
    window.removeEventListener(AUTH_CHANGED_EVENT, synchronize);
  };
}

export function isAuthenticated(): boolean {
  return getAuthSession() !== null;
}
