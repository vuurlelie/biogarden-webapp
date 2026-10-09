import { prepareSessionTransition } from "../utils/sessionTransition";
import type {
  AuthResponse,
  ConfirmEmailRequest,
  ForgotPasswordRequest,
  LoginRequest,
  RegisterRequest,
  ResetPasswordRequest,
  ResendEmailConfirmationRequest,
} from "../types/auth.types";
import { apiRequest } from "../../../shared/api/httpClient";

export class AuthApiError extends Error {
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = "AuthApiError";
    this.status = status;
  }
}

const AUTH_FALLBACK_MESSAGE = "A művelet nem sikerült. Kérjük, próbáld újra.";
const toAuthError = (message: string, status: number) => new AuthApiError(message, status);

export async function login(request: LoginRequest): Promise<AuthResponse> {
  await prepareSessionTransition();
  const response = await apiRequest("/api/auth/login", {
    fallbackMessage: AUTH_FALLBACK_MESSAGE,
    method: "POST",
    jsonBody: request,
    toError: toAuthError,
  });

  return response.json() as Promise<AuthResponse>;
}

export async function register(request: RegisterRequest): Promise<void> {
  await apiRequest("/api/auth/register", {
    fallbackMessage: AUTH_FALLBACK_MESSAGE,
    method: "POST",
    jsonBody: request,
    toError: toAuthError,
  });
}

export async function confirmEmail(request: ConfirmEmailRequest): Promise<void> {
  await apiRequest("/api/auth/confirm-email", {
    fallbackMessage: AUTH_FALLBACK_MESSAGE,
    method: "POST",
    jsonBody: request,
    toError: toAuthError,
  });
}

export async function resendEmailConfirmation(
  request: ResendEmailConfirmationRequest,
): Promise<void> {
  await apiRequest("/api/auth/resend-email-confirmation", {
    fallbackMessage: AUTH_FALLBACK_MESSAGE,
    method: "POST",
    jsonBody: request,
    toError: toAuthError,
  });
}

export async function forgotPassword(request: ForgotPasswordRequest): Promise<void> {
  await apiRequest("/api/auth/forgot-password", {
    fallbackMessage: AUTH_FALLBACK_MESSAGE,
    method: "POST",
    jsonBody: request,
    toError: toAuthError,
  });
}

export async function resetPassword(request: ResetPasswordRequest): Promise<void> {
  await apiRequest("/api/auth/reset-password", {
    fallbackMessage: AUTH_FALLBACK_MESSAGE,
    method: "POST",
    jsonBody: request,
    toError: toAuthError,
  });
}
