import {
  clearAuthSession,
  getAuthSession,
} from "../../auth/utils/authStorage";
import type { Order } from "../types/order.types";
import { TOO_MANY_REQUESTS_MESSAGE } from "../../../shared/api/httpClient";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

type ApiErrorBody = {
  message?: string;
  title?: string;
  detail?: string;
  Detail?: string;
  errors?: Record<string, string[]>;
};

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

async function getErrorMessage(response: Response): Promise<string> {
  if (response.status === 429) {
    return TOO_MANY_REQUESTS_MESSAGE;
  }

  try {
    const body = (await response.json()) as ApiErrorBody;
    const validationMessage = body.errors ? Object.values(body.errors).flat().join(" ") : undefined;
    return body.message ?? body.detail ?? body.Detail ?? validationMessage ?? body.title ?? "A művelet nem sikerült.";
  } catch {
    return "A művelet nem sikerült. Kérjük, próbáld újra.";
  }
}

async function ordersRequest(path = "", init: RequestInit = {}): Promise<Response> {
  const auth = getAuthSession();

  if (!auth) {
    throw new OrdersApiError("A munkamenet lejárt. Jelentkezz be újra.", 401);
  }

  const response = await fetch(`${API_BASE_URL}/api/orders${path}`, {
    ...init,
    headers: {
      Authorization: `Bearer ${auth.token}`,
      ...(init.body ? { "Content-Type": "application/json" } : {}),
      ...init.headers,
    },
  });

  if (response.status === 401) clearAuthSession();

  if (!response.ok) {
    throw new OrdersApiError(await getErrorMessage(response), response.status);
  }

  return response;
}

export async function getOrders(): Promise<Order[]> {
  const response = await ordersRequest();
  return response.json() as Promise<Order[]>;
}

export async function getOrderById(orderId: string): Promise<Order> {
  const response = await ordersRequest(`/${encodeURIComponent(orderId)}`);
  return response.json() as Promise<Order>;
}

export async function cancelOrder(orderId: string, reason: string): Promise<Order> {
  const response = await ordersRequest(`/${encodeURIComponent(orderId)}/cancel`, {
    method: "POST",
    body: JSON.stringify({ reason: reason.trim() }),
  });
  return response.json() as Promise<Order>;
}

export async function requestOrderWithdrawal(orderId: string, reason: string) {
  const response = await ordersRequest(`/${encodeURIComponent(orderId)}/withdrawal-requests`, {
    method: "POST",
    body: JSON.stringify({ reason: reason.trim() }),
  });
  return response.json() as Promise<Order["withdrawalRequests"][number]>;
}

export async function requestPublicOrderWithdrawal(request: {
  orderNumber: string;
  customerEmail: string;
  reason: string;
}) {
  const response = await fetch(`${API_BASE_URL}/api/orders/withdrawal-requests`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      orderNumber: request.orderNumber.trim(),
      customerEmail: request.customerEmail.trim(),
      reason: request.reason.trim(),
    }),
  });

  if (!response.ok) {
    throw new OrdersApiError(await getErrorMessage(response), response.status);
  }

  return response.json() as Promise<Order["withdrawalRequests"][number]>;
}

export async function createOrder(
  order: {
    idempotencyKey: string;
    addressId?: number | null;
    deliveryAddressId?: number | null;
    fulfillmentMethod: 0 | 1 | 2;
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
  },
): Promise<Order> {
  const auth = getAuthSession();

  const response = await fetch(`${API_BASE_URL}/api/orders`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      ...(auth ? { Authorization: `Bearer ${auth.token}` } : {}),
    },
    credentials: "include",
    body: JSON.stringify(order),
  });

  if (response.status === 401) clearAuthSession();
  if (!response.ok) {
    throw new OrdersApiError(await getErrorMessage(response), response.status);
  }

  return response.json() as Promise<Order>;
}

export async function createSumUpCheckout(orderId: string): Promise<SumUpCheckout> {
  const auth = getAuthSession();

  const response = await fetch(
    `${API_BASE_URL}/api/orders/${encodeURIComponent(orderId)}/sumup-checkout`,
    {
      method: "POST",
      headers: {
        ...(auth ? { Authorization: `Bearer ${auth.token}` } : {}),
      },
      credentials: "include",
    },
  );

  if (response.status === 401) clearAuthSession();
  if (!response.ok) {
    throw new OrdersApiError(await getErrorMessage(response), response.status);
  }

  return response.json() as Promise<SumUpCheckout>;
}

