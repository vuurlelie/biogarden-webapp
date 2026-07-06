export type OrderStatus = 0 | 1 | 2 | 3 | 4;
export type PaymentStatus = 0 | 1 | 2 | 3 | 4 | 5 | 6;
export type FulfillmentMethod = 0 | 1;

export type OrderItem = {
  productId: number;
  productName: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
};

export type Order = {
  id: string;
  orderNumber: string;
  userId: string;
  addressId: number;
  createdAtUtc: string;
  confirmedAtUtc: string | null;
  cancelledAtUtc: string | null;
  totalPrice: number;
  status: OrderStatus;
  paymentStatus: PaymentStatus;
  fulfillmentMethod: FulfillmentMethod;
  cancellationReason: string | null;
  items: OrderItem[];
};

