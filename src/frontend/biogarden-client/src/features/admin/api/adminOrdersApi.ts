import type { Order, OrderStatus, PaymentStatus } from "../../account/types/order.types";
import { adminRequest } from "./adminApiClient";

export async function getAdminOrders(signal?: AbortSignal): Promise<Order[]> {
  const response = await adminRequest("/orders", {
    signal,
  });
  return response.json() as Promise<Order[]>;
}

export async function updateAdminOrder(
  id: string,
  status: OrderStatus,
  paymentStatus: PaymentStatus,
  cancellationReason: string | null,
): Promise<Order> {
  const response = await adminRequest(`/orders/${id}`, {
    method: "PUT",
    jsonBody: { status, paymentStatus, cancellationReason },
  });
  return response.json() as Promise<Order>;
}
