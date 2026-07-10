export type AccountLifecycleRequestType = 1 | 2;
export type AccountLifecycleRequestStatus = 1 | 2 | 3;

export type AdminAccountLifecycleRequest = {
  id: number;
  type: AccountLifecycleRequestType;
  status: AccountLifecycleRequestStatus;
  reason: string | null;
  message: string;
  requestedAtUtc: string;
};

export type AdminUser = {
  id: string;
  email: string;
  firstName: string | null;
  lastName: string | null;
  phoneNumber: string | null;
  companyName: string | null;
  taxNumber: string | null;
  roles: string[];
  isAdmin: boolean;
  isDeactivated: boolean;
  isAnonymized: boolean;
  createdAt: string;
  orderCount: number;
  activeOrderCount: number;
  pendingRequest: AdminAccountLifecycleRequest | null;
};

export type AdminUserErasureResult = {
  userId: string;
  user: AdminUser | null;
  message: string;
};
