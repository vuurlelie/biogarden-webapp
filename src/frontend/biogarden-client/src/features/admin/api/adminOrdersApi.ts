import type { Order, OrderStatus, PaymentStatus } from "../../account/types/order.types";
import { getAuthSession } from "../../auth/utils/authStorage";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

function headers(): HeadersInit {
  const auth = getAuthSession();
  if (!auth) throw new Error("A munkamenet lejárt. Jelentkezz be újra.");
  return { Authorization: `Bearer ${auth.token}` };
}

async function ensure(response: Response) {
  if (response.ok) return;

  try {
    const body = await response.json() as { detail?: string; Detail?: string };
    throw new Error(body.detail ?? body.Detail ?? "A művelet nem sikerült.");
  } catch (error) {
    if (error instanceof Error && error.message !== "Unexpected end of JSON input") throw error;
    throw new Error("A művelet nem sikerült.");
  }
}

export async function getAdminOrders(signal?: AbortSignal): Promise<Order[]> {
  const response = await fetch(`${API_BASE_URL}/api/admin/orders`, {
    headers: headers(),
    signal,
  });
  await ensure(response);
  return response.json() as Promise<Order[]>;
}

export async function updateAdminOrder(
  id: string,
  status: OrderStatus,
  paymentStatus: PaymentStatus,
  cancellationReason: string | null,
): Promise<Order> {
  const response = await fetch(`${API_BASE_URL}/api/admin/orders/${id}`, {
    method: "PUT",
    headers: { ...headers(), "Content-Type": "application/json" },
    body: JSON.stringify({ status, paymentStatus, cancellationReason }),
  });
  await ensure(response);
  return response.json() as Promise<Order>;
}
