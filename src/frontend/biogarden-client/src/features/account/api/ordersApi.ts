import type { PagedResult } from "../../../shared/types/pagination";
import {
  clearAuthSession,
  getAuthSession,
} from "../../auth/utils/authStorage";
import type { Order } from "../types/order.types";
import type { FulfillmentMethod } from "../../checkout/constants/fulfillment";
import type { OrderPaymentMethod } from "../../checkout/constants/payment";
import { apiRequest, type ApiRequestOptions } from "../../../shared/api/httpClient";

export class OrdersApiError extends Error {
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = "OrdersApiError";
    this.status = status;
  }
}

export type SumUpCheckout = {
  orderId: string;
  checkoutId: string;
  url: string;
};

export type PublicOrderPaymentStatus = Pick<Order, "orderNumber" | "status" | "paymentStatus" | "confirmedAtUtc"> & {
  orderId: string;
};

export type CreateOrderRequest = {
  idempotencyKey: string;
  addressId?: number | null;
  deliveryAddressId?: number | null;
  fulfillmentMethod: FulfillmentMethod;
  paymentMethod: OrderPaymentMethod;
  quoteVersion: string;
  glsDeliveryPointId?: string | null;
  customerName?: string;
  customerEmail?: string;
  customerPhone?: string;
  postalCode?: string;
  city?: string;
  street?: string;
  houseNumber?: string;
  building?: string;
  floor?: string;
  door?: string;
  doorbell?: string;
  billingCompanyName?: string;
  billingTaxNumber?: string;
  customerType?: 0 | 1;
  printedInvoiceRequested?: boolean;
  acceptedTermsVersion?: string;
  acceptedPrivacyPolicyVersion?: string;
  deliveryPostalCode?: string;
  deliveryCity?: string;
  deliveryStreet?: string;
  deliveryHouseNumber?: string;
  deliveryBuilding?: string;
  deliveryFloor?: string;
  deliveryDoor?: string;
  deliveryDoorbell?: string;
};

const ORDERS_FALLBACK_MESSAGE = "A művelet nem sikerült. Kérjük, próbáld újra.";
export async function recoverCheckoutOrder(checkoutKey: string, signal?: AbortSignal): Promise<Order> {
  const response = await apiRequest(`/api/orders/checkout-recovery/${encodeURIComponent(checkoutKey)}`, {
    signal, authSessionId: getAuthSession()?.sessionId, credentials: "include", cache: "no-store",
    fallbackMessage: "A rendelés eredményét most nem tudjuk ellenőrizni.",
  });
  return response.json() as Promise<Order>;
}
export async function getCheckoutOrder(orderId: string, signal?: AbortSignal): Promise<Order> {
  const response = await apiRequest(`/api/orders/${encodeURIComponent(orderId)}/receipt`, {
    signal, authSessionId: getAuthSession()?.sessionId, credentials: "include", cache: "no-store",
    fallbackMessage: "A rendelés részletei ebben a munkamenetben nem érhetők el.",
  });
  return response.json() as Promise<Order>;
}
const toOrdersError = (message: string, status: number) => new OrdersApiError(message, status);

async function ordersRequest(
  path = "",
  init: Partial<ApiRequestOptions> = {}
): Promise<Response> {
  const auth = getAuthSession();

  if (!auth) {
    throw new OrdersApiError("A munkamenet lejárt. Jelentkezz be újra.", 401);
  }

  return apiRequest(`/api/orders${path}`, {
    fallbackMessage: ORDERS_FALLBACK_MESSAGE,
    ...init,
    authSessionId: auth.sessionId,
    onUnauthorized: clearAuthSession,
    toError: toOrdersError,
  });
}

export async function getOrders(query: { view?: string; page?: number; search?: string; year?: string; pageSize?: number } = {}): Promise<PagedResult<Order>> {
  const params = new URLSearchParams({ view: query.view ?? "recent", page: String(query.page ?? 1), pageSize: String(query.pageSize ?? 10) });
  if (query.search?.trim())
    params.set("search", query.search.trim());
  if (query.year)
    params.set("year", query.year);
  const response = await ordersRequest(`?${params}`);
  return response.json() as Promise<PagedResult<Order>>;
}

export async function getOrderById(orderId: string): Promise<Order> {
  const response = await ordersRequest(`/${encodeURIComponent(orderId)}`);
  return response.json() as Promise<Order>;
}

export async function cancelOrder(orderId: string, reason: string): Promise<Order> {
  const response = await ordersRequest(`/${encodeURIComponent(orderId)}/cancel`, {
    method: "POST",
    jsonBody: { reason: reason.trim() },
  });
  return response.json() as Promise<Order>;
}

export async function requestOrderWithdrawal(orderId: string, reason: string) {
  const response = await ordersRequest(`/${encodeURIComponent(orderId)}/withdrawal-requests`, {
    method: "POST",
    jsonBody: { reason: reason.trim() },
  });
  return response.json() as Promise<Order["withdrawalRequests"][number]>;
}

export async function requestPublicOrderWithdrawal(request: {
  orderNumber: string;
  customerEmail: string;
  reason: string;
}) {
  const response = await apiRequest(
    "/api/orders/withdrawal-requests",
    {
      fallbackMessage: ORDERS_FALLBACK_MESSAGE,
      method: "POST",
      jsonBody: {
        orderNumber: request.orderNumber.trim(),
        customerEmail: request.customerEmail.trim(),
        reason: request.reason.trim(),
      },
      toError: toOrdersError,
    }
  );

  return response.json() as Promise<Order["withdrawalRequests"][number]>;
}

export async function createOrder(order: CreateOrderRequest): Promise<Order> {
  const auth = getAuthSession();

  const response = await apiRequest(
    "/api/orders",
    {
      authSessionId: auth?.sessionId,
      fallbackMessage: ORDERS_FALLBACK_MESSAGE,
      method: "POST",
      credentials: "include",
      jsonBody: order,
      onUnauthorized: clearAuthSession,
      toError: toOrdersError,
    }
  );

  return response.json() as Promise<Order>;
}

export async function createSumUpCheckout(orderId: string): Promise<SumUpCheckout> {
  const auth = getAuthSession();

  const response = await apiRequest(`/api/orders/${encodeURIComponent(orderId)}/sumup-checkout`, {
    authSessionId: auth?.sessionId,
    fallbackMessage: ORDERS_FALLBACK_MESSAGE,
    method: "POST",
    credentials: "include",
    onUnauthorized: clearAuthSession,
    toError: toOrdersError,
  });

  return response.json() as Promise<SumUpCheckout>;
}

export async function getPublicOrderPaymentStatus(orderId: string, sumUpCheckoutReference: string): Promise<PublicOrderPaymentStatus> {
  const params = new URLSearchParams({ sumUpCheckoutReference });
  const response = await apiRequest(
    `/api/orders/${encodeURIComponent(orderId)}/payment-status?${params.toString()}`,
    {
      fallbackMessage: ORDERS_FALLBACK_MESSAGE,
      method: "GET",
      credentials: "include",
      toError: toOrdersError,
    },
  );

  return response.json() as Promise<PublicOrderPaymentStatus>;
}

