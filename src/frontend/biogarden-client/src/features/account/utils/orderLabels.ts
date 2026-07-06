import type {
  OrderStatus,
  PaymentStatus,
} from "../types/order.types";

const ORDER_STATUS_LABELS: Record<OrderStatus, string> = {
  0: "Függőben",
  1: "Megerősítve",
  2: "Szállítás alatt",
  3: "Teljesítve",
  4: "Törölve",
};

const PAYMENT_STATUS_LABELS: Record<PaymentStatus, string> = {
  0: "Fizetésre vár",
  1: "Engedélyezve",
  2: "Fizetve",
  3: "Sikertelen",
  4: "Visszatérítés folyamatban",
  5: "Visszatérítve",
  6: "Részben visszatérítve",
};

const PRICE_FORMATTER = new Intl.NumberFormat("hu-HU", {
  style: "currency",
  currency: "HUF",
  maximumFractionDigits: 0,
});

const DATE_FORMATTER = new Intl.DateTimeFormat("hu-HU", {
  year: "numeric",
  month: "long",
  day: "numeric",
  hour: "2-digit",
  minute: "2-digit",
});

export function getOrderStatusLabel(status: OrderStatus): string {
  return ORDER_STATUS_LABELS[status];
}

export function getPaymentStatusLabel(status: PaymentStatus): string {
  return PAYMENT_STATUS_LABELS[status];
}

export function formatOrderPrice(price: number): string {
  return PRICE_FORMATTER.format(price);
}

export function formatOrderDate(date: string): string {
  return DATE_FORMATTER.format(new Date(date));
}

export function getOrderStatusClass(status: OrderStatus): string {
  if (status === 4) return "bg-site-danger-soft text-site-danger";
  if (status === 3) return "bg-site-surface-soft text-site-green-dark";
  return "bg-site-surface-warm text-site-heading";
}

