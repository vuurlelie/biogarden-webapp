import { adminListQuery, type AdminListQuery } from "./adminListQuery";
import type { PagedResult } from "../../../shared/types/pagination";
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
  adminResponse?: string | null;
};

export type AdminOrderRequestThread = { orderId: string; messages: AdminOrderWithdrawalRequest[] };

export async function getAdminOrders(query: AdminListQuery, signal?: AbortSignal): Promise<PagedResult<Order>> {
  const response = await adminRequest(`/orders${adminListQuery(query)}`, {
    signal,
  });
  return response.json() as Promise<PagedResult<Order>>;
}

export async function updateAdminOrder(
  id: string,
  status: OrderStatus,
  paymentStatus: PaymentStatus,
  cancellationReason: string | null,
  version: string,
  paymentCorrectionReason: string | null,
): Promise<Order> {
  const response = await adminRequest(`/orders/${id}`, {
    method: "PUT",
    jsonBody: { status, paymentStatus, cancellationReason, version, paymentCorrectionReason },
  });
  return response.json() as Promise<Order>;
}

export async function getAdminOrderWithdrawalRequests(query: AdminListQuery, signal?: AbortSignal): Promise<PagedResult<AdminOrderRequestThread>> {
  const response = await adminRequest(`/orders/withdrawal-requests${adminListQuery(query)}`, {
    fallbackMessage: "Az elállási vagy fogyasztói kérések betöltése nem sikerült.",
    signal,
  });
  return response.json() as Promise<PagedResult<AdminOrderRequestThread>>;
}

export async function markAdminOrderWithdrawalRequestHandled(id: number, adminResponse?: string): Promise<AdminOrderWithdrawalRequest> {
  const response = await adminRequest(`/orders/withdrawal-requests/${id}/handled`, {
    method: "PUT",
    jsonBody: { adminResponse },
    fallbackMessage: "A kérés feldolgozottra állítása nem sikerült.",
  });
  return response.json() as Promise<AdminOrderWithdrawalRequest>;
}

export async function getAdminOrder(id: string): Promise<Order> {
  const response = await adminRequest(`/orders/${id}`);
  return response.json() as Promise<Order>;
}
