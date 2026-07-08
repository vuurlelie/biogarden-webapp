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

