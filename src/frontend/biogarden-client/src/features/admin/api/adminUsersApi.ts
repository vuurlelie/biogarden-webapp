import { getAuthSession } from "../../auth/utils/authStorage";
import type { AdminUser, AdminUserErasureResult } from "../types/users.types";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

function headers(json = false): HeadersInit {
  const auth = getAuthSession();
  if (!auth) throw new Error("A munkamenet lejárt. Jelentkezz be újra.");
  return {
    Authorization: `Bearer ${auth.token}`,
    ...(json ? { "Content-Type": "application/json" } : {}),
  };
}

async function ensure(response: Response) {
  if (response.ok) return;

  try {
    const body = await response.json() as { detail?: string; Detail?: string };
    throw new Error(body.detail ?? body.Detail ?? "A művelet nem sikerült.");
  } catch (error) {
    if (error instanceof Error && error.message !== "Unexpected end of JSON input") throw error;
    throw new Error("A művelet nem sikerült.");
  }
}

export async function getAdminUsers(signal?: AbortSignal): Promise<AdminUser[]> {
  const response = await fetch(`${API_BASE_URL}/api/admin/users`, {
    headers: headers(),
    signal,
  });
  await ensure(response);
  return response.json() as Promise<AdminUser[]>;
}

export async function setAdminRole(userId: string, isAdmin: boolean): Promise<AdminUser> {
  const response = await fetch(`${API_BASE_URL}/api/admin/users/${userId}/admin-role?isAdmin=${isAdmin}`, {
    method: "POST",
    headers: headers(),
  });
  await ensure(response);
  return response.json() as Promise<AdminUser>;
}

export async function deactivateAdminUser(userId: string): Promise<AdminUser> {
  const response = await fetch(`${API_BASE_URL}/api/admin/users/${userId}/deactivate`, {
    method: "POST",
    headers: headers(),
  });
  await ensure(response);
  return response.json() as Promise<AdminUser>;
}

export async function reactivateAdminUser(userId: string): Promise<AdminUser> {
  const response = await fetch(`${API_BASE_URL}/api/admin/users/${userId}/reactivate`, {
    method: "POST",
    headers: headers(),
  });
  await ensure(response);
  return response.json() as Promise<AdminUser>;
}

export async function eraseAdminUser(userId: string): Promise<AdminUserErasureResult> {
  const response = await fetch(`${API_BASE_URL}/api/admin/users/${userId}/erase`, {
    method: "POST",
    headers: headers(),
  });
  await ensure(response);
  return response.json() as Promise<AdminUserErasureResult>;
}

export async function completeErasureRequest(requestId: number, adminNote: string | null): Promise<AdminUser> {
  const response = await fetch(`${API_BASE_URL}/api/admin/users/requests/${requestId}/complete-erasure`, {
    method: "POST",
    headers: headers(true),
    body: JSON.stringify({ adminNote }),
  });
  await ensure(response);
  return response.json() as Promise<AdminUser>;
}

export async function rejectLifecycleRequest(requestId: number, adminNote: string | null): Promise<AdminUser> {
  const response = await fetch(`${API_BASE_URL}/api/admin/users/requests/${requestId}/reject`, {
    method: "POST",
    headers: headers(true),
    body: JSON.stringify({ adminNote }),
  });
  await ensure(response);
  return response.json() as Promise<AdminUser>;
}
