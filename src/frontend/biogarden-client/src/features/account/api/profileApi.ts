import {
  clearAuthSession,
  getAuthSession,
} from "../../auth/utils/authStorage";
import type {
  AccountLifecycleRequest,
  AccountLifecycleResult,
  UpdateUserProfile,
  UserProfile,
} from "../types/profile.types";
import type {
  UpdateUserAddresses,
  UserAddresses,
} from "../types/address.types";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

type ApiErrorBody = {
  detail?: string;
  Detail?: string;
};

export class ProfileApiError extends Error {
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = "ProfileApiError";
    this.status = status;
  }
}

async function getErrorMessage(response: Response): Promise<string> {
  try {
    const body = (await response.json()) as ApiErrorBody;
    return body.detail ?? body.Detail ?? "A művelet nem sikerült.";
  } catch {
    return "A művelet nem sikerült. Kérjük, próbáld újra.";
  }
}

async function profileRequest(
  path = "",
  init?: RequestInit,
): Promise<Response> {
  const auth = getAuthSession();

  if (!auth) {
    throw new ProfileApiError("A munkamenet lejárt. Jelentkezz be újra.", 401);
  }

  const response = await fetch(`${API_BASE_URL}/api/profile${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${auth.token}`,
      ...init?.headers,
    },
  });

  if (response.status === 401) clearAuthSession();

  if (!response.ok) {
    throw new ProfileApiError(await getErrorMessage(response), response.status);
  }

  return response;
}

export async function getProfile(): Promise<UserProfile> {
  const response = await profileRequest();
  return response.json() as Promise<UserProfile>;
}

export async function updateProfile(
  profile: UpdateUserProfile,
): Promise<void> {
  await profileRequest("", { method: "PUT", body: JSON.stringify(profile) });
}

export async function getAddresses(): Promise<UserAddresses> {
  const response = await profileRequest("/addresses");
  return response.json() as Promise<UserAddresses>;
}

export async function updateAddresses(
  addresses: UpdateUserAddresses,
): Promise<void> {
  await profileRequest("/addresses", {
    method: "PUT",
    body: JSON.stringify(addresses),
  });
}

export async function requestAccountLifecycle(
  request: AccountLifecycleRequest,
): Promise<AccountLifecycleResult> {
  const response = await profileRequest("/account-lifecycle", {
    method: "POST",
    body: JSON.stringify(request),
  });
  return response.json() as Promise<AccountLifecycleResult>;
}
