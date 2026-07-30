import type { Order, OrderStatus, PaymentStatus } from "../../account/types/order.types";
import { adminRequest } from "./adminApiClient";

export type AdminOrderWithdrawalRequest = {
  id: number;
  orderId: string;
  orderNumber: string;
  orderStatus: OrderStatus;
  orderTotalPrice: number;
  customerName: string;
  customerEmail: string;
  reason: string;
  status: 1 | 2;
  requestedAtUtc: string;
  handledAtUtc: string | null;
};

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

export async function getAdminOrderWithdrawalRequests(signal?: AbortSignal): Promise<AdminOrderWithdrawalRequest[]> {
  const response = await adminRequest("/orders/withdrawal-requests", {
    fallbackMessage: "Az elállási vagy fogyasztói kérések betöltése nem sikerült.",
    signal,
  });
  return response.json() as Promise<AdminOrderWithdrawalRequest[]>;
}

export async function markAdminOrderWithdrawalRequestHandled(id: number): Promise<AdminOrderWithdrawalRequest> {
  const response = await adminRequest(`/orders/withdrawal-requests/${id}/handled`, {
    method: "PUT",
    fallbackMessage: "A kérés feldolgozottra állítása nem sikerült.",
  });
  return response.json() as Promise<AdminOrderWithdrawalRequest>;
}
