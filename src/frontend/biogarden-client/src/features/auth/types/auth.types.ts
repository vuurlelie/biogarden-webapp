export type LoginRequest = {
  email: string;
  password: string;
};

export type RegisterRequest = LoginRequest & {
  confirmPassword: string;
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
  token: string;
};

