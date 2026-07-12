import type { UnitOfMeasure } from "../../products/types/product.types";

export type OrderStatus = 0 | 1 | 2 | 3 | 4;
export type PaymentStatus = 0 | 1 | 2 | 3 | 4 | 5 | 6;
export type FulfillmentMethod = 0 | 1 | 2;

export type OrderItem = {
  productId: number;
  productName: string;
  unitPrice: number;
  unit: UnitOfMeasure;
  quantity: number;
  lineTotal: number;
};

export type Order = {
  id: string;
  orderNumber: string;
  userId: string | null;
  addressId: number | null;
  customerName: string;
  customerEmail: string;
  customerPhone: string;
  postalCode: string;
  city: string;
  street: string;
  houseNumber: string;
  building: string | null;
  floor: string | null;
  door: string | null;
  doorbell: string | null;
  billingCompanyName: string | null;
  billingTaxNumber: string | null;
  deliveryPostalCode: string | null;
  deliveryCity: string | null;
  deliveryStreet: string | null;
  deliveryHouseNumber: string | null;
  deliveryBuilding: string | null;
  deliveryFloor: string | null;
  deliveryDoor: string | null;
  deliveryDoorbell: string | null;
  createdAtUtc: string;
  confirmedAtUtc: string | null;
  cancelledAtUtc: string | null;
  deletedAtUtc: string | null;
  totalPrice: number;
  status: OrderStatus;
  paymentStatus: PaymentStatus;
  fulfillmentMethod: FulfillmentMethod;
  stripeCheckoutSessionId: string | null;
  stripePaymentIntentId: string | null;
  cancellationReason: string | null;
  items: OrderItem[];
};

