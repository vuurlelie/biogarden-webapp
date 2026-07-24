import type { UserAddresses } from "../../account/types/address.types";
import type { UserProfile } from "../../account/types/profile.types";
import type { CheckoutDraft } from "../types/checkout.types";

export const CHECKOUT_DRAFT_KEY = "biogarden_checkout_draft";
export const CHECKOUT_IDEMPOTENCY_KEY = "biogarden_checkout_idempotency_key";

export const EMPTY_DRAFT: CheckoutDraft = {
  customerName: "",
  customerEmail: "",
  customerPhone: "",
  postalCode: "",
  city: "",
  street: "",
  houseNumber: "",
  building: "",
  floor: "",
  door: "",
  doorbell: "",
  customerType: 0,
  billingCompanyName: "",
  billingTaxNumber: "",
  printedInvoiceRequested: false,
  deliveryPostalCode: "",
  deliveryCity: "",
  deliveryStreet: "",
  deliveryHouseNumber: "",
  deliveryBuilding: "",
  deliveryFloor: "",
  deliveryDoor: "",
  deliveryDoorbell: "",
};

export function loadDraft(): CheckoutDraft {
  try {
    return { ...EMPTY_DRAFT, ...JSON.parse(sessionStorage.getItem(CHECKOUT_DRAFT_KEY) ?? "{}") };
  } catch {
    return EMPTY_DRAFT;
  }
}

export function getOrCreateCheckoutIdempotencyKey(): string {
  const existing = sessionStorage.getItem(CHECKOUT_IDEMPOTENCY_KEY);
  if (existing) return existing;

  const generated = crypto.randomUUID().replaceAll("-", "");
  sessionStorage.setItem(CHECKOUT_IDEMPOTENCY_KEY, generated);
  return generated;
}

export function buildCustomerName(profile: UserProfile): string {
  return [profile.lastName, profile.firstName].filter(Boolean).join(" ");
}

export function billingAddressDraft(
  address: NonNullable<UserAddresses["billingAddress"]>,
): Partial<CheckoutDraft> {
  return {
    postalCode: address.postalCode,
    city: address.city,
    street: address.street,
    houseNumber: address.houseNumber,
    building: address.building ?? "",
    floor: address.floor ?? "",
    door: address.door ?? "",
    doorbell: address.doorbell ?? "",
  };
}

export function deliveryAddressDraft(
  address: NonNullable<UserAddresses["shippingAddress"]>,
): Partial<CheckoutDraft> {
  return {
    deliveryPostalCode: address.postalCode,
    deliveryCity: address.city,
    deliveryStreet: address.street,
    deliveryHouseNumber: address.houseNumber,
    deliveryBuilding: address.building ?? "",
    deliveryFloor: address.floor ?? "",
    deliveryDoor: address.door ?? "",
    deliveryDoorbell: address.doorbell ?? "",
  };
}

export function emptyCustomerDraft(): Partial<CheckoutDraft> {
  return {
    customerName: "",
    customerEmail: "",
    customerPhone: "",
  };
}

export function emptyBillingDraft(): Partial<CheckoutDraft> {
  return {
    billingCompanyName: "",
    billingTaxNumber: "",
    postalCode: "",
    city: "",
    street: "",
    houseNumber: "",
    building: "",
    floor: "",
    door: "",
    doorbell: "",
  };
}

export function emptyDeliveryDraft(): Partial<CheckoutDraft> {
  return {
    deliveryPostalCode: "",
    deliveryCity: "",
    deliveryStreet: "",
    deliveryHouseNumber: "",
    deliveryBuilding: "",
    deliveryFloor: "",
    deliveryDoor: "",
    deliveryDoorbell: "",
  };
}
