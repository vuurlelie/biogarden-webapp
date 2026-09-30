import type { RefObject } from "react";
import type { Cart } from "../../cart/types/cart.types";
import type { CheckoutStep, FulfillmentMethod, PaymentMethod } from "../types/checkout.types";
import { PRICE } from "../utils/checkoutFormatting";
import { SummaryCartItem } from "./SummaryCartItem";
import { FULFILLMENT_LABELS } from "../constants/fulfillment";

type CheckoutOrderSummaryProps = {
  cart: Cart;
  deliveryFee: number;
  grandTotal: number;
  method: FulfillmentMethod;
  paymentFee: number;
  paymentMethod: PaymentMethod | null;
  reviewButtonRef: RefObject<HTMLButtonElement | null>;
  step: CheckoutStep;
  submitting: boolean;
  className?: string;
};

export function CheckoutOrderSummary({
  cart,
  className = "",
  deliveryFee,
  grandTotal,
  method,
  paymentFee,
  paymentMethod,
  reviewButtonRef,
  step,
  submitting,
}: CheckoutOrderSummaryProps) {
  return (
    <aside className={`rounded-sm bg-site-surface-soft p-5 shadow-md lg:sticky lg:top-28 ${className}`}>
      <h2 className="text-xl font-bold">Rendelés összesítése</h2>
      <ul className="mt-4 divide-y divide-site-border border-b border-site-border pb-4">
        {cart.items.map((item) => (
          <SummaryCartItem key={item.id} item={item} />
        ))}
      </ul>

      {deliveryFee > 0 && (
        <div className="mt-3 flex justify-between text-sm">
          <span>{FULFILLMENT_LABELS[method]}</span>
          <strong>{PRICE.format(deliveryFee)}</strong>
        </div>
      )}
      {paymentFee > 0 && (
        <div className="mt-2 flex justify-between text-sm">
          <span>Utánvét</span>
          <strong>{PRICE.format(paymentFee)}</strong>
        </div>
      )}

      <div className="mt-4 flex justify-between border-t border-site-border pt-4 text-lg font-bold">
        <span>Összesen</span>
        <span>{PRICE.format(grandTotal)}</span>
      </div>

      <button
        ref={reviewButtonRef}
        type="submit"
        disabled={submitting || (step === "payment" && paymentMethod === null)}
        className="mt-5 w-full rounded-sm bg-site-green px-5 py-3 font-semibold text-white disabled:opacity-50"
      >
        {submitting ? "Ellenőrzés..." : step === "details" ? "Tovább a pénztárhoz" : "Rendelés áttekintése"}
      </button>

      {step === "payment" && paymentMethod === null && (
        <p className="mt-2 text-xs text-site-muted">Válassz fizetési módot a folytatáshoz.</p>
      )}
    </aside>
  );
}
