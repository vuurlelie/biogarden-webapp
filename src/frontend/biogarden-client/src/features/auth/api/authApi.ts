import type {
  AuthResponse,
  LoginRequest,
  RegisterRequest,
} from "../types/auth.types";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

type ApiErrorBody = {
  detail?: string;
  Detail?: string;
};

export class AuthApiError extends Error {
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = "AuthApiError";
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

export async function login(request: LoginRequest): Promise<AuthResponse> {
  const response = await fetch(`${API_BASE_URL}/api/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    throw new AuthApiError(await getErrorMessage(response), response.status);
  }

  return response.json() as Promise<AuthResponse>;
}

export async function register(request: RegisterRequest): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/auth/register`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    throw new AuthApiError(await getErrorMessage(response), response.status);
  }
}
