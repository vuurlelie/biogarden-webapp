import type { AuthResponse } from "../types/auth.types";

export const AUTH_STORAGE_KEY = "biogarden_auth";

export function saveAuthSession(auth: AuthResponse): void {
  localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(auth));
}

