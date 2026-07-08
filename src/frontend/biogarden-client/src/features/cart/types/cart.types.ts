import type { UnitOfMeasure } from "../../products/types/product.types";

export type CartItem = {
  id: string;
  productId: number;
  productName: string;
  unitPrice: number;
  image: string | null;
  unit: UnitOfMeasure;
  quantity: number;
  stockQuantity: number;
  reservedQuantityExcludingCurrentCart: number;
  availableQuantityForCart: number;
  available: boolean;
  deliveryAvailable: boolean;
  lineTotal: number;
};

export type Cart = {
  id: string;
  userId: string | null;
  sessionId: string | null;
  createdAtUtc: string;
  lastActivityAtUtc: string;
  expiresAtUtc: string;
  isCheckedOut: boolean;
  totalItemCount: number;
  totalPrice: number;
  items: CartItem[];
};

export type CartValidationIssue = {
  productId?: number;
  productName?: string;
  message: string;
};

export type CartValidationResult = {
  isValid: boolean;
  issues: CartValidationIssue[];
};
