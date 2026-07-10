export type UserProfile = {
  email: string;
  firstName: string | null;
  lastName: string | null;
  phoneNumber: string | null;
  companyName: string | null;
  taxNumber: string | null;
};

export type UpdateUserProfile = {
  firstName: string | null;
  lastName: string | null;
  phoneNumber: string | null;
  companyName: string | null;
  taxNumber: string | null;
};

export type AccountLifecycleRequestType = 1 | 2;

export type AccountLifecycleRequest = {
  type: AccountLifecycleRequestType;
  reason: string | null;
};

export type AccountLifecycleResult = {
  message: string;
  sessionEnded: boolean;
  status: 1 | 2 | 3;
};

