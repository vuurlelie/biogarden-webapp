import { apiRequest } from "../../../shared/api/httpClient";
import { getAuthSession } from "../../auth/utils/authStorage";
import type { OrderItem } from "../../account/types/order.types";
import { FULFILLMENT, type FulfillmentMethod } from "../constants/fulfillment";
import { PAYMENT, type OrderPaymentMethod } from "../constants/payment";
import type { GlsDeliveryPoint } from "./glsApi";

export type CheckoutMethod = { fulfillmentMethod: FulfillmentMethod; deliveryFee: number; paymentMethods: OrderPaymentMethod[] };
export type CheckoutQuote = { cartId: string; fulfillmentMethod: FulfillmentMethod; paymentMethod: OrderPaymentMethod;
  currency: string; subtotal: number; deliveryFee: number; paymentFee: number; totalPrice: number;
  version: string; items: OrderItem[]; deliveryPoint: GlsDeliveryPoint | null };

export async function getCheckoutMethods(signal: AbortSignal): Promise<CheckoutMethod[]> {
  const response = await apiRequest("/api/orders/checkout-methods", { signal, cache: "no-store", fallbackMessage: "Az átvételi módok nem tölthetők be." });
  const methods = await response.json() as CheckoutMethod[];
  if (!Array.isArray(methods) || methods.some(method => !method || !Object.values(FULFILLMENT).includes(method.fulfillmentMethod)
    || !Number.isFinite(method.deliveryFee) || method.deliveryFee < 0 || !Array.isArray(method.paymentMethods)
    || method.paymentMethods.some(payment => !Object.values(PAYMENT).includes(payment)))) throw new Error("Érvénytelen átvételi beállítások.");
  return methods;
}

export async function getCheckoutQuote(cartId: string, fulfillmentMethod: FulfillmentMethod, paymentMethod: OrderPaymentMethod,
  glsDeliveryPointId: string | null, signal: AbortSignal): Promise<CheckoutQuote> {
  const response = await apiRequest("/api/orders/quote", { method: "POST", signal, credentials: "include", cache: "no-store",
    authSessionId: getAuthSession()?.sessionId, jsonBody: { fulfillmentMethod, paymentMethod, glsDeliveryPointId },
    fallbackMessage: "Az árak ellenőrzése nem sikerült. Próbáld újra." });
  const quote = await response.json() as CheckoutQuote;
  if (!quote || quote.cartId !== cartId || quote.fulfillmentMethod !== fulfillmentMethod || quote.paymentMethod !== paymentMethod
    || quote.currency !== "HUF" || !/^[A-F0-9]{64}$/.test(quote.version)
    || ![quote.subtotal, quote.deliveryFee, quote.paymentFee, quote.totalPrice].every(value => Number.isFinite(value) && value >= 0)
    || !Array.isArray(quote.items) || !quote.items.length
    || quote.items.some(item => !item || !Number.isFinite(item.lineTotal) || !Number.isFinite(item.unitPrice) || !Number.isInteger(item.quantity) || item.quantity <= 0)
    || (glsDeliveryPointId !== null && (quote.deliveryPoint?.id !== glsDeliveryPointId || quote.deliveryPoint.type !== fulfillmentMethod))) {
    throw new Error("Érvénytelen árösszesítés. Próbáld újra.");
  }
  return quote;
}
