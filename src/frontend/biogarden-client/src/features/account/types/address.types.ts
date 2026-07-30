import type { AddressFormData } from "../../../shared/types/address.types";

export type AddressDetails = {
  id: number;
  postalCode: string;
  city: string;
  street: string;
  houseNumber: string;
  building: string | null;
  floor: string | null;
  door: string | null;
  doorbell: string | null;
  fullAddress: string;
};

export type { AddressFormData };

export type UserAddresses = {
  shippingAddress: AddressDetails | null;
  billingAddress: AddressDetails | null;
};

export type UpdateUserAddresses = {
  shippingAddress: AddressFormData | null;
  billingAddress: AddressFormData | null;
  useShippingAsBilling: boolean;
};

