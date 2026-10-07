import type { AuthResponse } from "../types/auth.types";

export const AUTH_STORAGE_KEY = "biogarden_auth";
export const AUTH_CHANGED_EVENT = "biogarden:auth-changed";
let session: AuthResponse | null = null;
let generation = 0;
let pending: Promise<void> | null = null;
const channel = typeof BroadcastChannel !== "undefined" ? new BroadcastChannel("biogarden-session") : null;

export function parseAuthSession(value: string | null): AuthResponse | null {
  try {
    const data = JSON.parse(value ?? "null") as AuthResponse | null;
    return data && typeof data.userId === "string" && typeof data.email === "string" &&
      typeof data.sessionId === "string" && data.sessionId.length > 0 && Array.isArray(data.roles) &&
      data.roles.every(role => typeof role === "string") && Number.isFinite(Date.parse(data.expiresAtUtc))
      ? data : null;
  } catch { return null; }
}

function update(auth: AuthResponse | null) {
  const changed = session?.sessionId !== auth?.sessionId;
  session = auth;
  if (changed) {
    generation++;
    try {
      sessionStorage.removeItem("biogarden_checkout_draft");
      sessionStorage.removeItem("biogarden_checkout_idempotency_key");
    } catch { /* Legacy cleanup is best effort. */ }
  }
  window.dispatchEvent(new Event(changed ? AUTH_CHANGED_EVENT : "biogarden:session-updated"));
}

export function saveAuthSession(auth: AuthResponse): void {
  update(auth);
  channel?.postMessage("changed");
}

// Used for expired/rejected sessions. Explicit logout is a separate server operation.
export function clearAuthSession(): void { update(null); }
export function getAuthSession(): AuthResponse | null { return session; }
export function isAuthenticated(): boolean { return session !== null; }

export async function refreshAuthSession(activity = false): Promise<void> {
  if (pending) return pending;
  const current = generation;
  pending = (async () => {
    const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/api/auth/session`, {
      credentials: "include", cache: "no-store",
      headers: { "X-Checkout-Request": "1", ...(activity ? { "X-Session-Activity": "1" } : {}) },
    });
    if (!response.ok) throw new Error("A munkamenet ellenőrzése nem sikerült.");
    const next = response.status === 204 ? null : parseAuthSession(await response.text());
    if (generation === current) update(next);
  })().finally(() => { pending = null; });
  return pending;
}

export async function logout(): Promise<void> {
  const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/api/auth/logout`, {
    method: "POST", credentials: "include",
    headers: { "X-Checkout-Request": "1", "X-Browser-Session": session?.sessionId ?? "guest" },
  });
  if (!response.ok) throw new Error("A kijelentkezés nem sikerült. Kérjük, próbáld újra.");
  update(null);
  channel?.postMessage("changed");
}

export function startAuthSessionSync(): () => void {
  try {
    localStorage.removeItem(AUTH_STORAGE_KEY);
    sessionStorage.removeItem("biogarden_checkout_draft");
    sessionStorage.removeItem("biogarden_checkout_idempotency_key");
  } catch { /* Authentication no longer depends on web storage. */ }
  let timer: ReturnType<typeof setTimeout> | undefined;
  let lastActivity = 0;
  const verify = () => { void refreshAuthSession().catch(() => undefined); };
  const schedule = () => {
    clearTimeout(timer);
    if (session) timer = setTimeout(verify, Math.max(1000, Date.parse(session.expiresAtUtc) - Date.now()));
  };
  const activity = (event: Event) => {
    if (!event.isTrusted || document.visibilityState !== "visible" || !session || Date.now() - lastActivity < 60_000) return;
    lastActivity = Date.now();
    void refreshAuthSession(true).catch(() => undefined);
  };
  const events = ["pointerdown", "keydown", "scroll"];
  for (const event of events) window.addEventListener(event, activity, { passive: true, capture: true });
  window.addEventListener("focus", verify);
  window.addEventListener(AUTH_CHANGED_EVENT, schedule);
  window.addEventListener("biogarden:session-updated", schedule);
  channel?.addEventListener("message", verify);
  schedule();
  return () => {
    clearTimeout(timer);
    for (const event of events) window.removeEventListener(event, activity, true);
    window.removeEventListener("focus", verify);
    window.removeEventListener(AUTH_CHANGED_EVENT, schedule);
    window.removeEventListener("biogarden:session-updated", schedule);
    channel?.removeEventListener("message", verify);
  };
}
