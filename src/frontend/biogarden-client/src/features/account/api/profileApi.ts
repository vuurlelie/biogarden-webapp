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
import { apiRequest, type ApiRequestOptions } from "../../../shared/api/httpClient";

export class ProfileApiError extends Error {
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = "ProfileApiError";
    this.status = status;
  }
}

const PROFILE_FALLBACK_MESSAGE = "A művelet nem sikerült. Kérjük, próbáld újra.";
const toProfileError = (message: string, status: number) => new ProfileApiError(message, status);

async function profileRequest(path = "", init?: Partial<ApiRequestOptions>): Promise<Response> {
  const auth = getAuthSession();

  if (!auth) {
    throw new ProfileApiError("A munkamenet lejárt. Jelentkezz be újra.", 401);
  }

  return apiRequest(`/api/profile${path}`, {
    fallbackMessage: PROFILE_FALLBACK_MESSAGE,
    ...init,
    authSessionId: auth.sessionId,
    onUnauthorized: clearAuthSession,
    toError: toProfileError,
  });
}

export async function getProfile(): Promise<UserProfile> {
  const response = await profileRequest();
  return response.json() as Promise<UserProfile>;
}

export async function updateProfile(
  profile: UpdateUserProfile,
): Promise<void> {
  await profileRequest(
    "",
    { method: "PUT", jsonBody: profile }
  );
}

export async function getAddresses(): Promise<UserAddresses> {
  const response = await profileRequest("/addresses");
  return response.json() as Promise<UserAddresses>;
}

export async function updateAddresses(
  addresses: UpdateUserAddresses,
): Promise<void> {
  await profileRequest(
    "/addresses",
    {
      method: "PUT",
      jsonBody: addresses,
    }
  );
}

export async function requestAccountLifecycle(
  request: AccountLifecycleRequest,
): Promise<AccountLifecycleResult> {
  const response = await profileRequest(
    "/account-lifecycle",
    {
      method: "POST",
      jsonBody: request,
    }
  );
  return response.json() as Promise<AccountLifecycleResult>;
}
