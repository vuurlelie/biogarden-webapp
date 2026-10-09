import { useEffect, useMemo, useState } from "react";
import type { Cart } from "../../cart/types/cart.types";
import { getCheckoutMethods, getCheckoutQuote, type CheckoutMethod, type CheckoutQuote } from "../api/checkoutPricingApi";
import type { FulfillmentMethod, PaymentMethod } from "../types/checkout.types";
import { PAYMENT_CODES } from "../constants/payment";
import { getErrorMessage } from "../../../shared/utils/errorHandling";

export function useCheckoutPricing(cart: Cart | null, method: FulfillmentMethod, payment: PaymentMethod | null, pointId: string | null, eligible: boolean) {
  const [attempt, setAttempt] = useState(0);
  const [methods, setMethods] = useState<CheckoutMethod[] | null>(null);
  const [methodsError, setMethodsError] = useState("");
  const hasItems = Boolean(cart?.items.length);
  const key = useMemo(
    () => ({ cart, method, payment, pointId, eligible, attempt }),
    [cart, method, payment, pointId, eligible, attempt]
  );
  const [result, setResult] = useState<{ key: typeof key; quote?: CheckoutQuote; error?: string } | null>(null);
  useEffect(
    () => {
      if (!hasItems)
        return;
      const controller = new AbortController();
      void getCheckoutMethods(controller.signal).then(value => {
        if (!controller.signal.aborted) {
          setMethods(value);
          setMethodsError("");
        }
      }).catch(error => {
        if (!controller.signal.aborted)
          setMethodsError(getErrorMessage(error, "Az átvételi módok nem tölthetők be."));
      });
      return () => controller.abort();
    },
    [attempt, hasItems]
  );
  const allowed = Boolean(payment && methods?.some(choice => choice.fulfillmentMethod === method && choice.paymentMethods.includes(PAYMENT_CODES[payment])));
  useEffect(
    () => {
      if (!cart?.items.length || !payment || !eligible || !allowed || methodsError)
        return;
      const controller = new AbortController();
      void getCheckoutQuote(cart.id, method, PAYMENT_CODES[payment], pointId, controller.signal).then(quote => {
        if (!controller.signal.aborted)
          setResult({ key, quote });
      }).catch(error => {
        if (!controller.signal.aborted)
          setResult({ key, error: getErrorMessage(error, "Az árak ellenőrzése nem sikerült.") });
      });
      return () => controller.abort();
    },
    [cart, method, payment, pointId, eligible, allowed, methodsError, key]
  );
  const current = result?.key === key && eligible && allowed && !methodsError ? result : null;
  return { methods, methodsError, quote: current?.quote ?? null, error: current?.error,
    loading: Boolean(payment && eligible && allowed && !methodsError && !current), retry: () => setAttempt(value => value + 1) };
}
