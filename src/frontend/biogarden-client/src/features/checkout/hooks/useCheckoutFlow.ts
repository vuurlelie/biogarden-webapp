import { useEffect, useRef, useState, type FormEvent } from "react";
import { useNavigate, useSearchParams } from "react-router";
import { createOrder, createSumUpCheckout } from "../../account/api/ordersApi";
import { useAnalytics } from "../../analytics/context/AnalyticsContextCore";
import { getCartValueBucket } from "../../analytics/utils/eventValidation";
import { validateCart } from "../../cart/api/cartApi";
import type { CartContextValue } from "../../cart/context/useCart";
import type { useCheckoutForm } from "./useCheckoutForm";
import type { useCheckoutPricing } from "./useCheckoutPricing";
import type { useGlsDeliveryPoint } from "./useGlsDeliveryPoint";
import { PAYMENT } from "../constants/payment";
import { isGlsMethod } from "../constants/fulfillment";
import type { CheckoutDraft, CheckoutFieldErrors, CheckoutStep, PaymentMethod } from "../types/checkout.types";
import { buildCheckoutOrderRequest } from "../utils/checkoutOrderPayload";
import { validateCheckoutFields } from "../utils/checkoutValidation";
import { ROUTES } from "../../../shared/constants/routes";
import { useModalFocus } from "../../../shared/hooks/useModalFocus";
import { getErrorMessage, getServerFieldErrors, hasErrorStatus } from "../../../shared/utils/errorHandling";

type CheckoutFlowOptions = {
  form: ReturnType<typeof useCheckoutForm>;
  cartState: CartContextValue;
  pricing: ReturnType<typeof useCheckoutPricing>;
  gls: ReturnType<typeof useGlsDeliveryPoint>;
  paymentMethod: PaymentMethod | null;
  setPaymentMethod: (method: PaymentMethod | null) => void;
};

export function useCheckoutFlow({ form, cartState, pricing, gls, paymentMethod, setPaymentMethod }: CheckoutFlowOptions) {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const { cart, isLoading: cartLoading, refresh } = cartState;
  const { draftStorage, draft, draftReady, method, loading, setError, setFieldErrors, effectiveCustomerType } = form;
  const quote = pricing.quote;
  const [submitting, setSubmitting] = useState(false);
  const [rejectedCheckoutKey, setRejectedCheckoutKey] = useState<string | null>(null);
  const [showReview, setShowReview] = useState(false);
  const [termsAccepted, setTermsAccepted] = useState(false);
  const [privacyAccepted, setPrivacyAccepted] = useState(false);
  const [step, setStep] = useState<CheckoutStep>("details");
  const reviewModalRef = useRef<HTMLElement | null>(null);
  const reviewButtonRef = useRef<HTMLButtonElement | null>(null);
  const checkoutStartedTrackedRef = useRef(false);
  const { track } = useAnalytics();
  const cancelledSumUpOrderId = searchParams.get("sumup") === "cancelled"
    ? searchParams.get("orderId")
    : null;
  const checkoutKey = searchParams.get("checkoutKey") === rejectedCheckoutKey ? null : searchParams.get("checkoutKey");

  useModalFocus(reviewModalRef, {
    isOpen: showReview && Boolean(quote) && (submitting || (!cancelledSumUpOrderId && !checkoutKey)) && !loading && !cartLoading && draftReady && Boolean(cart?.items.length),
    onClose: () => { if (!submitting) setShowReview(false); },
    returnFocusRef: reviewButtonRef,
  });

  useEffect(() => {
    setPaymentMethod(null);
    setShowReview(false);
    setTermsAccepted(false);
    setPrivacyAccepted(false);
  }, [method, draft.glsDeliveryPointId, setPaymentMethod]);

  useEffect(() => {
    setShowReview(false);
    setTermsAccepted(false);
    setPrivacyAccepted(false);
  }, [quote?.version, paymentMethod]);

  useEffect(() => {
    if (!cart?.items.length || checkoutStartedTrackedRef.current) return;

    checkoutStartedTrackedRef.current = true;
    track("checkout_started", {
      itemCount: cart.totalItemCount,
      cartValueBucket: getCartValueBucket(cart.totalPrice),
    });
  }, [cart, track]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setError("");

    if (!cart?.items.length) {
      setError("A kosár üres.");
      return;
    }

    const errors = validateCheckoutFields(draft, effectiveCustomerType, method);
    if (!pricing.methods?.some(choice => choice.fulfillmentMethod === method) || pricing.methodsError) {
      setError(pricing.methodsError || "Válassz egy elérhető átvételi módot.");
      return;
    }
    if (step === "payment" && !quote) { setError("Ellenőrizd újra az árakat a folytatáshoz."); return; }
    if (isGlsMethod(method) && !gls.point) {
      errors.glsDeliveryPointId = gls.loading ? "Várd meg az átvételi pont ellenőrzését." : gls.error ?? "Válassz egy GLS átvételi pontot a térképen.";
    }
    setFieldErrors(errors);
    if (Object.keys(errors).length) {
      requestAnimationFrame(() => {
        const firstInvalidField = document.querySelector<HTMLElement>('[aria-invalid="true"]');
        firstInvalidField?.scrollIntoView({ behavior: "smooth", block: "center" });
        firstInvalidField?.focus({ preventScroll: true });
      });
      return;
    }

    setSubmitting(true);
    try {
      const validation = await validateCart();
      if (!validation.isValid) {
        setError(validation.issues.map((issue) => issue.message).join(" "));
        await refresh();
        return;
      }

      if (step === "details") {
        setStep("payment");
        track("checkout_step_completed", {
          step: "details",
          fulfillmentMethod: String(method),
        });
      } else {
        setShowReview(true);
        track("checkout_step_completed", {
          step: "payment",
          fulfillmentMethod: String(method),
          paymentMethod,
        });
      }
    } catch (validationError) {
      setError(getErrorMessage(validationError, "A kosár ellenőrzése nem sikerült."));
    } finally {
      setSubmitting(false);
    }
  }

  async function discardDraft() {
    if (!await form.discard()) return;
    setTermsAccepted(false);
    setPrivacyAccepted(false);
    setFieldErrors({});
    setStep("details");
  }

  async function placeOrder() {
    if (submitting) return;
    if (!quote) { setShowReview(false); setError("Az árösszesítés megváltozott. Tekintsd át újra a rendelést."); return; }
    if (!cart?.items.length) {
      setError("A kosár üres.");
      setShowReview(false);
      return;
    }

    const consentErrors: CheckoutFieldErrors = {};
    if (!termsAccepted) {
      consentErrors.terms = "Az Általános Szerződési Feltételek elfogadása kötelező.";
    }
    if (!privacyAccepted) {
      consentErrors.privacy = "Az Adatkezelési Tájékoztató elfogadása kötelező.";
    }

    setFieldErrors((current) => ({ ...current, ...consentErrors }));
    if (Object.keys(consentErrors).length) {
      requestAnimationFrame(() => reviewModalRef.current?.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus());
      return;
    }

    setSubmitting(true);
    setError("");

    let orderCreated = false;
    let requestStarted = false;
    let submissionKey: string | null = null;
    try {
      await draftStorage.pause();
      const request = buildCheckoutOrderRequest(draft, effectiveCustomerType, method, cart.id, quote);
      submissionKey = request.idempotencyKey;
      setRejectedCheckoutKey(null);
      // Save only the server-owned cart key in the URL before submitting; no personal data.
      setSearchParams({ checkoutKey: request.idempotencyKey }, { replace: true });
      requestStarted = true;
      const order = await createOrder(request);
      orderCreated = true;

      track("order_submitted", {
        paymentMethod,
        fulfillmentMethod: String(method),
        itemCount: cart.totalItemCount,
        cartValueBucket: getCartValueBucket(cart.totalPrice),
      });

      if (order.paymentMethod === PAYMENT.OnlineCard) {
        // Retain only the order identifier in the URL, before contacting the provider.
        // The backend still checks ownership; a failed payment start must not resubmit the cart.
        setSearchParams({ sumup: "cancelled", orderId: order.id }, { replace: true });
        const checkout = await createSumUpCheckout(order.id);
        window.location.assign(checkout.url);
        return;
      }

        navigate(ROUTES.orderSuccess, { replace: true, state: { order } });
      void refresh().catch(() => undefined);
    } catch (submitError) {
      const rejected = [400, 401, 403, 404, 409, 422, 429].some(status => hasErrorStatus(submitError, status));
      if (!orderCreated && (!requestStarted || rejected)) {
        // Router transitions can lag state updates. Keep the existing form mounted for focus restoration.
        setRejectedCheckoutKey(submissionKey);
        setSearchParams({}, { replace: true });
        draftStorage.resume();
      }
      if (!orderCreated && hasErrorStatus(submitError, 409)) {
        pricing.retry();
        setTermsAccepted(false);
        setPrivacyAccepted(false);
      }
      setShowReview(false);
      const serverErrors = getServerFieldErrors<keyof CheckoutFieldErrors>(submitError,
        [...Object.keys(draft) as (keyof CheckoutDraft)[], "terms", "privacy"],
        { acceptedTermsVersion: "terms", acceptedPrivacyPolicyVersion: "privacy" });
      setFieldErrors(serverErrors);
      if (Object.keys(serverErrors).some((field) => field !== "terms" && field !== "privacy")) {
        setStep("details");
      }
      setError(getErrorMessage(submitError, "A rendelést nem sikerült elküldeni."));
    } finally {
      setSubmitting(false);
    }
  }

  return { submitting, showReview, setShowReview, termsAccepted, setTermsAccepted,
    privacyAccepted, setPrivacyAccepted, step, setStep, reviewModalRef, reviewButtonRef,
    cancelledSumUpOrderId, checkoutKey, submit, discardDraft, placeOrder };
}
