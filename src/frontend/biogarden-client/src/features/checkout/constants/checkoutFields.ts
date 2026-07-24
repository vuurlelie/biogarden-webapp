import type { CheckoutDraft, CheckoutFieldDefinition } from "../types/checkout.types";

export const CONTACT_FIELDS: CheckoutFieldDefinition[] = [
  ["customerName", "Név", "name"],
  ["customerEmail", "Email cím", "email"],
  ["customerPhone", "Telefonszám", "tel"],
];

export const BILLING_FIELDS: CheckoutFieldDefinition[] = [
  ["billingCompanyName", "Cégnév", "organization"],
  ["billingTaxNumber", "Adószám (céges számlához)", "off"],
  ["postalCode", "Irányítószám", "postal-code"],
  ["city", "Település", "address-level2"],
  ["street", "Közterület neve és jellege", "address-line1"],
  ["houseNumber", "Házszám", "address-line2"],
  ["building", "Épület", "off"],
  ["floor", "Emelet", "off"],
  ["door", "Ajtó", "off"],
  ["doorbell", "Kapucsengő", "off"],
];

export const DELIVERY_FIELDS: CheckoutFieldDefinition[] = [
  ["deliveryPostalCode", "Irányítószám", "shipping postal-code"],
  ["deliveryCity", "Település", "shipping address-level2"],
  ["deliveryStreet", "Közterület neve és jellege", "shipping address-line1"],
  ["deliveryHouseNumber", "Házszám", "shipping address-line2"],
  ["deliveryBuilding", "Épület", "off"],
  ["deliveryFloor", "Emelet", "off"],
  ["deliveryDoor", "Ajtó", "off"],
  ["deliveryDoorbell", "Kapucsengő", "off"],
];

export const REQUIRED_BILLING_FIELDS: Array<keyof CheckoutDraft> = [
  "billingCompanyName",
  "billingTaxNumber",
  "postalCode",
  "city",
  "street",
  "houseNumber",
];

export const REQUIRED_DELIVERY_FIELDS: Array<keyof CheckoutDraft> = [
  "deliveryPostalCode",
  "deliveryCity",
  "deliveryStreet",
  "deliveryHouseNumber",
];
