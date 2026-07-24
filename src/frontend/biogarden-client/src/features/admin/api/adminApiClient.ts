import { apiRequest, type ApiRequestOptions } from "../../../shared/api/httpClient";
import { clearAuthSession, getAuthSession } from "../../auth/utils/authStorage";

const ADMIN_FALLBACK_MESSAGE = "A művelet nem sikerült.";

export function adminRequest(
  path: string,
  {
    fallbackMessage = ADMIN_FALLBACK_MESSAGE,
    ...init
  }: Partial<ApiRequestOptions> = {},
): Promise<Response> {
  const auth = getAuthSession();

  if (!auth) {
    throw new Error("A munkamenet lejárt. Jelentkezz be újra.");
  }

  return apiRequest(`/api/admin${path}`, {
    fallbackMessage,
    ...init,
    authToken: auth.token,
    onUnauthorized: clearAuthSession,
  });
}
