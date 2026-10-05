import { adminListQuery, type AdminListQuery } from "./adminListQuery";
import type { PagedResult } from "../../../shared/types/pagination";
import type { AdminUser, AdminUserErasureResult } from "../types/users.types";
import { adminRequest } from "./adminApiClient";

export async function getAdminUsers(query: AdminListQuery, signal?: AbortSignal): Promise<PagedResult<AdminUser>> {
  const response = await adminRequest(`/users${adminListQuery(query)}`, {
    signal,
  });
  return response.json() as Promise<PagedResult<AdminUser>>;
}

export async function setAdminRole(userId: string, isAdmin: boolean): Promise<AdminUser> {
  const response = await adminRequest(`/users/${userId}/admin-role?isAdmin=${isAdmin}`, {
    method: "POST",
  });
  return response.json() as Promise<AdminUser>;
}

export async function confirmAdminUserEmail(userId: string): Promise<AdminUser> {
  const response = await adminRequest(`/users/${userId}/confirm-email`, {
    method: "POST",
  });
  return response.json() as Promise<AdminUser>;
}

export async function deactivateAdminUser(userId: string): Promise<AdminUser> {
  const response = await adminRequest(`/users/${userId}/deactivate`, {
    method: "POST",
  });
  return response.json() as Promise<AdminUser>;
}

export async function reactivateAdminUser(userId: string): Promise<AdminUser> {
  const response = await adminRequest(`/users/${userId}/reactivate`, {
    method: "POST",
  });
  return response.json() as Promise<AdminUser>;
}

export async function eraseAdminUser(userId: string): Promise<AdminUserErasureResult> {
  const response = await adminRequest(`/users/${userId}/erase`, {
    method: "POST",
  });
  return response.json() as Promise<AdminUserErasureResult>;
}

export async function completeErasureRequest(requestId: number, adminNote: string | null): Promise<AdminUser> {
  const response = await adminRequest(`/users/requests/${requestId}/complete-erasure`, {
    method: "POST",
    jsonBody: { adminNote },
  });
  return response.json() as Promise<AdminUser>;
}

export async function rejectLifecycleRequest(requestId: number, adminNote: string | null): Promise<AdminUser> {
  const response = await adminRequest(`/users/requests/${requestId}/reject`, {
    method: "POST",
    jsonBody: { adminNote },
  });
  return response.json() as Promise<AdminUser>;
}
