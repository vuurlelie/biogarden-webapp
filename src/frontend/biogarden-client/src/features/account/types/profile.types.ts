export type UserProfile = {
  email: string;
  customerType: 0 | 1;
  firstName: string | null;
  lastName: string | null;
  phoneNumber: string | null;
  companyName: string | null;
  taxNumber: string | null;
  printedInvoiceRequested: boolean;
};

export type UpdateUserProfile = {
  customerType: 0 | 1;
  firstName: string | null;
  lastName: string | null;
  phoneNumber: string | null;
  companyName: string | null;
  taxNumber: string | null;
  printedInvoiceRequested: boolean;
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

