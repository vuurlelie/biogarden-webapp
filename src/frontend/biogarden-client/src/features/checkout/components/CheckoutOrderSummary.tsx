import type { RefObject } from "react";
import type { Cart } from "../../cart/types/cart.types";
import type { CheckoutStep, FulfillmentMethod, PaymentMethod } from "../types/checkout.types";
import { PRICE } from "../utils/checkoutFormatting";
import { SummaryCartItem } from "./SummaryCartItem";
import { FULFILLMENT_LABELS } from "../constants/fulfillment";
import { PriceBreakdown } from "./PriceBreakdown";
import type { CheckoutQuote } from "../api/checkoutPricingApi";

type CheckoutOrderSummaryProps = {
  cart: Cart;
  quote: CheckoutQuote | null;
  pricingLoading: boolean;
  pricingError?: string;
  onRetryPricing: () => void;
  method: FulfillmentMethod;
  paymentMethod: PaymentMethod | null;
  reviewButtonRef: RefObject<HTMLButtonElement | null>;
  step: CheckoutStep;
  submitting: boolean;
  className?: string;
};

export function CheckoutOrderSummary({
  cart,
  className = "",
  quote,
  pricingLoading,
  pricingError,
  onRetryPricing,
  method,
  paymentMethod,
  reviewButtonRef,
  step,
  submitting,
}: CheckoutOrderSummaryProps) {
  return (
    <aside className={`rounded-sm bg-site-surface-soft p-5 shadow-md lg:sticky lg:top-28 ${className}`}>
      <h2 className="text-xl font-bold">Rendelés összesítése</h2>
      <ul className="mt-4 divide-y divide-site-border border-b border-site-border pb-4">
        {(quote?.items ?? cart.items).map((item) => (
          <SummaryCartItem key={item.productId} item={{ ...cart.items.find(line => line.productId === item.productId), ...item }} />
        ))}
      </ul>

      <p className="my-3 text-sm">{FULFILLMENT_LABELS[method]}</p>
      {quote ? <PriceBreakdown price={quote} /> : <p className="my-3 text-sm">Termékek összege: {PRICE.format(cart.totalPrice)}. A végösszeget az átvételi és fizetési mód kiválasztása után ellenőrizzük.</p>}
      {pricingLoading && <p role="status">Árak ellenőrzése...</p>}
      {pricingError && <div role="alert"><p>{pricingError}</p><button type="button" onClick={onRetryPricing} className="underline">Árak újraellenőrzése</button></div>}

      <button
        ref={reviewButtonRef}
        type="submit"
        disabled={submitting || (step === "payment" && (paymentMethod === null || quote === null))}
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
