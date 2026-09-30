import type { FulfillmentMethod } from "../constants/fulfillment";
export type { FulfillmentMethod } from "../constants/fulfillment";
export type PaymentMethod = "online-card" | "pay-at-pickup" | "cash-on-delivery";
export type CheckoutStep = "details" | "payment";

export type CheckoutDraft = {
  fulfillmentMethod: FulfillmentMethod;
  glsDeliveryPointId: string | null;
  customerName: string;
  customerEmail: string;
  customerPhone: string;
  postalCode: string;
  city: string;
  street: string;
  houseNumber: string;
  building: string;
  floor: string;
  door: string;
  doorbell: string;
  customerType: 0 | 1;
  billingCompanyName: string;
  billingTaxNumber: string;
  printedInvoiceRequested: boolean;
  deliveryPostalCode: string;
  deliveryCity: string;
  deliveryStreet: string;
  deliveryHouseNumber: string;
  deliveryBuilding: string;
  deliveryFloor: string;
  deliveryDoor: string;
  deliveryDoorbell: string;
};

export type CheckoutFieldErrors = Partial<
  Record<
    keyof CheckoutDraft | "accountContact" | "billingAddress" | "deliveryAddress" | "terms" | "privacy",
    string
  >
>;

export type CheckoutDraftTextField = {
  [Key in keyof CheckoutDraft]: CheckoutDraft[Key] extends string ? Key : never;
}[keyof CheckoutDraft];

export type CheckoutFieldDefinition = [CheckoutDraftTextField, string, string];
