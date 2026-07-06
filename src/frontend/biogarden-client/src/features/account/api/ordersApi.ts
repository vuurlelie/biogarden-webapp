import {
  clearAuthSession,
  getAuthSession,
} from "../../auth/utils/authStorage";
import type { Order } from "../types/order.types";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

type ApiErrorBody = {
  detail?: string;
  Detail?: string;
};

export class OrdersApiError extends Error {
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = "OrdersApiError";
    this.status = status;
  }
}

async function getErrorMessage(response: Response): Promise<string> {
  try {
    const body = (await response.json()) as ApiErrorBody;
    return body.detail ?? body.Detail ?? "A művelet nem sikerült.";
  } catch {
    return "A művelet nem sikerült. Kérjük, próbáld újra.";
  }
}

async function ordersRequest(path = ""): Promise<Response> {
  const auth = getAuthSession();

  if (!auth) {
    throw new OrdersApiError("A munkamenet lejárt. Jelentkezz be újra.", 401);
  }

  const response = await fetch(`${API_BASE_URL}/api/orders${path}`, {
    headers: { Authorization: `Bearer ${auth.token}` },
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

export async function createOrder(
  addressId: number,
  fulfillmentMethod: 0 | 1,
): Promise<Order> {
  const auth = getAuthSession();

  if (!auth) {
    throw new OrdersApiError("A rendeléshez be kell jelentkezni.", 401);
  }

  const response = await fetch(`${API_BASE_URL}/api/orders`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${auth.token}`,
    },
    credentials: "include",
    body: JSON.stringify({ addressId, fulfillmentMethod }),
  });

  if (response.status === 401) clearAuthSession();
  if (!response.ok) {
    throw new OrdersApiError(await getErrorMessage(response), response.status);
  }

  return response.json() as Promise<Order>;
}

