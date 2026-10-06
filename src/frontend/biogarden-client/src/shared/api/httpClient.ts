import { clearAuthSession, getAuthSession } from "../../features/auth/utils/authStorage";

export const TOO_MANY_REQUESTS_MESSAGE =
  "Túl sok kérés érkezett. Kérjük, várj egy percet, majd próbáld újra.";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

type ApiErrorBody = {
  message?: string;
  title?: string;
  detail?: string;
  Detail?: string;
  errors?: Record<string, string[]>;
  traceId?: string;
};

export type ApiErrorDetails = {
  fieldErrors: Record<string, string[]>;
  traceId?: string;
};

export class ApiRequestError extends Error {
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = "ApiRequestError";
    this.status = status;
  }
}

export type ApiRequestOptions = Omit<RequestInit, "body"> & {
  authToken?: string;
  body?: BodyInit | null;
  fallbackMessage: string;
  jsonBody?: unknown;
  onUnauthorized?: () => void;
  throwOnError?: boolean;
  toError?: (message: string, status: number) => Error;
};

export function buildApiUrl(path: string): string {
  return `${API_BASE_URL}${path}`;
}

export async function getApiErrorMessage(
  response: Response,
  fallbackMessage: string,
): Promise<string> {
  return (await readApiError(response, fallbackMessage)).message;
}

async function readApiError(response: Response, fallbackMessage: string): Promise<ApiErrorDetails & { message: string }> {
  let body: ApiErrorBody = {};
  try {
    const parsed: unknown = await response.json();
    if (parsed !== null && typeof parsed === "object") body = parsed as ApiErrorBody;
  } catch {
    // Proxy errors and empty responses need a usable fallback too.
  }
  const fieldErrors: Record<string, string[]> = {};
  if (body.errors && typeof body.errors === "object" && !Array.isArray(body.errors)) {
    for (const [key, value] of Object.entries(body.errors)) {
      if (!Array.isArray(value)) continue;
      const messages = value.filter((item): item is string => typeof item === "string" && item.length > 0);
      if (messages.length) fieldErrors[key] = messages;
    }
  }
  const validationMessage = Object.values(fieldErrors).flat().join(" ");
  const message = [validationMessage, body.message, body.detail, body.Detail, body.title]
    .find((value) => typeof value === "string" && value.length > 0) ?? fallbackMessage;
  return {
    message: response.status === 429 ? TOO_MANY_REQUESTS_MESSAGE : message,
    fieldErrors,
    traceId: typeof body.traceId === "string" ? body.traceId : undefined,
  };
}

export async function apiRequest(
  path: string,
  {
    authToken,
    body,
    fallbackMessage,
    headers: requestHeaders,
    jsonBody,
    onUnauthorized,
    throwOnError = true,
    toError = (message, status) => new ApiRequestError(message, status),
    ...init
  }: ApiRequestOptions,
): Promise<Response> {
  const headers = new Headers(requestHeaders);
  if (/^\/api\/(cart|orders|checkout-drafts)(\/|$)/i.test(path)) {
    headers.set("X-Checkout-Request", "1");
  }

  if (authToken) headers.set("Authorization", `Bearer ${authToken}`);
  if (jsonBody !== undefined) headers.set("Content-Type", "application/json");

  const response = await fetch(buildApiUrl(path), {
    ...init,
    body: jsonBody !== undefined ? JSON.stringify(jsonBody) : body,
    headers,
  });

  // An old response must neither clear a newer login nor populate its screens.
  if (authToken && getAuthSession()?.token !== authToken) {
    throw new DOMException("The session changed while the request was running.", "AbortError");
  }
  if (response.status === 401 && authToken) {
    (onUnauthorized ?? clearAuthSession)();
  }

  if (throwOnError && !response.ok) {
    const { message, ...details } = await readApiError(response, fallbackMessage);
    // Preserve feature-specific error classes/status handling while retaining structured validation.
    throw Object.assign(toError(message, response.status), details);
  }

  return response;
}
