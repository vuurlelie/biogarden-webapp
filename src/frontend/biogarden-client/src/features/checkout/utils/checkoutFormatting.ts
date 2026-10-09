import type { PaymentMethod } from "../types/checkout.types";

export const PRICE = new Intl.NumberFormat(
  "hu-HU",
  {
    style: "currency",
    currency: "HUF",
    maximumFractionDigits: 0,
  }
);

export function paymentLabel(paymentMethod: PaymentMethod | null): string {
  if (paymentMethod === "pay-at-pickup")
    return "Fizetés átvételkor";
  if (paymentMethod === "cash-on-delivery")
    return "Utánvét";
  return "Online bankkártya";
}
