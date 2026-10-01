export const PAYMENT = { OnlineCard: 0, PayAtPickup: 1, CashOnDelivery: 2 } as const;
export type OrderPaymentMethod = typeof PAYMENT[keyof typeof PAYMENT];
export const PAYMENT_CODES = { "online-card": PAYMENT.OnlineCard, "pay-at-pickup": PAYMENT.PayAtPickup, "cash-on-delivery": PAYMENT.CashOnDelivery } as const;
export const PAYMENT_LABELS: Record<OrderPaymentMethod, string> = {
  [PAYMENT.OnlineCard]: "Online bankkártyás fizetés", [PAYMENT.PayAtPickup]: "Fizetés átvételkor", [PAYMENT.CashOnDelivery]: "Utánvét",
};
