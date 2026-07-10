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

export type AuthResponse = {
  userId: string;
  email: string;
  roles: string[];
  token: string;
};

