import type { AddressFormData } from "../../account/types/address.types";

export type LoginRequest = {
  email: string;
  password: string;
};

export type RegisterRequest = LoginRequest & {
  confirmPassword: string;
  customerType: 0 | 1;
  companyName: string | null;
  taxNumber: string | null;
  billingAddress: AddressFormData | null;
  printedInvoiceRequested: boolean;
};

export type ConfirmEmailRequest = {
  userId: string;
  token: string;
};

export type ResendEmailConfirmationRequest = {
  email: string;
};

export type ForgotPasswordRequest = {
  email: string;
};

export type ResetPasswordRequest = {
  userId: string;
  token: string;
  password: string;
  confirmPassword: string;
};

export type AuthResponse = {
  userId: string;
  email: string;
  roles: string[];
  sessionId: string;
  expiresAtUtc: string;
};

