export type UserProfile = {
  email: string;
  firstName: string | null;
  lastName: string | null;
  phoneNumber: string | null;
};

export type UpdateUserProfile = {
  firstName: string | null;
  lastName: string | null;
  phoneNumber: string | null;
};

