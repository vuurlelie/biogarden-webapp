import type {
  AuthResponse,
  ConfirmEmailRequest,
  ForgotPasswordRequest,
  LoginRequest,
  RegisterRequest,
  ResetPasswordRequest,
  ResendEmailConfirmationRequest,
} from "../types/auth.types";
import { getApiErrorMessage } from "../../../shared/api/httpClient";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

export class AuthApiError extends Error {
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = "AuthApiError";
    this.status = status;
  }
}

async function getErrorMessage(response: Response): Promise<string> {
  return getApiErrorMessage(
    response,
    "A művelet nem sikerült. Kérjük, próbáld újra.",
  );
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

export async function confirmEmail(request: ConfirmEmailRequest): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/auth/confirm-email`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    throw new AuthApiError(await getErrorMessage(response), response.status);
  }
}

export async function resendEmailConfirmation(
  request: ResendEmailConfirmationRequest,
): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/auth/resend-email-confirmation`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    throw new AuthApiError(await getErrorMessage(response), response.status);
  }
}

export async function forgotPassword(request: ForgotPasswordRequest): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/auth/forgot-password`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    throw new AuthApiError(await getErrorMessage(response), response.status);
  }
}

export async function resetPassword(request: ResetPasswordRequest): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/auth/reset-password`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    throw new AuthApiError(await getErrorMessage(response), response.status);
  }
}
