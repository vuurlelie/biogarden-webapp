export const TOO_MANY_REQUESTS_MESSAGE =
  "Túl sok kérés érkezett. Kérjük, várj egy percet, majd próbáld újra.";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

type ApiErrorBody = {
  message?: string;
  title?: string;
  detail?: string;
  Detail?: string;
  errors?: Record<string, string[]>;
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
  if (response.status === 429) {
    return TOO_MANY_REQUESTS_MESSAGE;
  }

  try {
    const body = (await response.json()) as ApiErrorBody;
    const validationMessage = body.errors
      ? Object.values(body.errors).flat().join(" ")
      : undefined;

    return body.message
      ?? body.detail
      ?? body.Detail
      ?? validationMessage
      ?? body.title
      ?? fallbackMessage;
  } catch {
    return fallbackMessage;
  }
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

  if (authToken) headers.set("Authorization", `Bearer ${authToken}`);
  if (jsonBody !== undefined) headers.set("Content-Type", "application/json");

  const response = await fetch(buildApiUrl(path), {
    ...init,
    body: jsonBody !== undefined ? JSON.stringify(jsonBody) : body,
    headers,
  });

  if (response.status === 401) onUnauthorized?.();

  if (throwOnError && !response.ok) {
    throw toError(await getApiErrorMessage(response, fallbackMessage), response.status);
  }

  return response;
}
