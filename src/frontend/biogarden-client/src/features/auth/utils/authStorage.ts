import type { AuthResponse } from "../types/auth.types";

export const AUTH_STORAGE_KEY = "biogarden_auth";

export function saveAuthSession(auth: AuthResponse): void {
  localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(auth));
}

export function clearAuthSession(): void {
  localStorage.removeItem(AUTH_STORAGE_KEY);
}

function isTokenExpired(token: string): boolean {
  try {
    const payloadPart = token.split(".")[1];
    if (!payloadPart) return true;

    const normalizedPayload = payloadPart
      .replace(/-/g, "+")
      .replace(/_/g, "/")
      .padEnd(Math.ceil(payloadPart.length / 4) * 4, "=");
    const payload = JSON.parse(atob(normalizedPayload)) as { exp?: number };

    return typeof payload.exp !== "number" || payload.exp * 1000 <= Date.now();
  } catch {
    return true;
  }
}

export function getAuthSession(): AuthResponse | null {
  const storedAuth = localStorage.getItem(AUTH_STORAGE_KEY);
  if (!storedAuth) return null;

  try {
    const auth = JSON.parse(storedAuth) as Partial<AuthResponse>;

    if (
      typeof auth.userId !== "string" ||
      typeof auth.email !== "string" ||
      typeof auth.token !== "string" ||
      !Array.isArray(auth.roles) ||
      isTokenExpired(auth.token)
    ) {
      clearAuthSession();
      return null;
    }

    return auth as AuthResponse;
  } catch {
    clearAuthSession();
    return null;
  }
}

export function isAuthenticated(): boolean {
  return getAuthSession() !== null;
}
