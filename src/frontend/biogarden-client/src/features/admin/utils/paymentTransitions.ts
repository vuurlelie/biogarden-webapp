import type { Order, PaymentStatus } from "../../account/types/order.types";
import { PAYMENT } from "../../checkout/constants/payment";

export const PAYMENT_STATUS = { Pending: 0, Authorized: 1, Paid: 2, Failed: 3, RefundPending: 4, Refunded: 5, PartiallyRefunded: 6 } as const;
export const ORDER_STATUS = { Pending: 0, Confirmed: 1, Shipped: 2, Completed: 3, Cancelled: 4 } as const;

export function manualPaymentChoices(order: Order): PaymentStatus[] {
  const status = order.paymentStatus;
  const choices: PaymentStatus[] = [status];
  switch (status) {
    case PAYMENT_STATUS.Pending:
    case PAYMENT_STATUS.Authorized:
    case PAYMENT_STATUS.Failed:
      if (order.paymentMethod !== PAYMENT.OnlineCard && order.status !== ORDER_STATUS.Cancelled)
        choices.push(PAYMENT_STATUS.Paid);
      break;
    case PAYMENT_STATUS.Paid: choices.push(PAYMENT_STATUS.RefundPending); break;
    case PAYMENT_STATUS.RefundPending: choices.push(PAYMENT_STATUS.PartiallyRefunded, PAYMENT_STATUS.Refunded); break;
    case PAYMENT_STATUS.PartiallyRefunded: choices.push(PAYMENT_STATUS.RefundPending, PAYMENT_STATUS.Refunded); break;
  }
  return choices;
}
