import { useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import { Link, useNavigate } from "react-router";
import { useCart } from "../../features/cart/context/useCart";
import { useCheckoutForm } from "../../features/checkout/hooks/useCheckoutForm";
import { useCheckoutFlow } from "../../features/checkout/hooks/useCheckoutFlow";
import { useGlsDeliveryPoint } from "../../features/checkout/hooks/useGlsDeliveryPoint";
import { useCheckoutPricing } from "../../features/checkout/hooks/useCheckoutPricing";
import { PAYMENT_CODES, PAYMENT_LABELS } from "../../features/checkout/constants/payment";
import { GlsPointPicker } from "../../features/checkout/components/GlsPointPicker";
import { FULFILLMENT, FULFILLMENT_LABELS, isGlsMethod } from "../../features/checkout/constants/fulfillment";
import { CheckoutOrderSummary } from "../../features/checkout/components/CheckoutOrderSummary";
import { CheckoutReviewModal } from "../../features/checkout/components/CheckoutReviewModal";
import { CheckoutEmptyCartView, CheckoutLoadingView } from "../../features/checkout/components/CheckoutStatusViews";
import { CheckoutRecoveryView } from "../../features/checkout/components/CheckoutRecoveryView";
import { CONTACT_FIELDS } from "../../features/checkout/constants/checkoutFields";
import type { PaymentMethod } from "../../features/checkout/types/checkout.types";
import { emptyDeliveryDraft } from "../../features/checkout/utils/checkoutDraft";
import { PRICE } from "../../features/checkout/utils/checkoutFormatting";
import { ROUTES } from "../../shared/constants/routes";
import AddressFields from "../../shared/components/forms/AddressFields";

export default function CheckoutPage() {
  const headingRef = useRef<HTMLHeadingElement | null>(null);
  const navigate = useNavigate();
  const [leaving, setLeaving] = useState(false);
  const cartState = useCart();
  const { cart, isLoading: cartLoading } = cartState;
  const form = useCheckoutForm(cart?.items.length ? cart.id : undefined);
  const { draftStorage, draft, setDraft, draftReady, method, authenticated, addresses,
    contactManual, billingManual, deliveryManual, loading, error, fieldErrors, setFieldErrors,
    effectiveCustomerType, billingAddress, deliveryAddress, setMethod, update, fieldClass, clearFieldErrors,
    handleBillingAddressChange, handleDeliveryAddressChange, handleAccountContactChange,
    handleAccountBillingChange, handleAccountDeliveryChange } = form;
  const gls = useGlsDeliveryPoint(draft.glsDeliveryPointId, method);
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod | null>(null);
  const deliveryAllowed = useMemo(
    () => Boolean(cart?.items.length && cart.items.every((item) => item.deliveryAvailable)),
    [cart],
  );
  const pricing = useCheckoutPricing(cart, method, paymentMethod, isGlsMethod(method) ? draft.glsDeliveryPointId : null,
    draftReady && (method === FULFILLMENT.PersonalPickup || deliveryAllowed) && (!isGlsMethod(method) || Boolean(gls.point)));
  const quote = pricing.quote;
  const availablePayments = pricing.methods?.find(choice => choice.fulfillmentMethod === method)?.paymentMethods ?? [];
  const { submitting, showReview, setShowReview, termsAccepted, setTermsAccepted,
    privacyAccepted, setPrivacyAccepted, step, setStep, reviewModalRef, reviewButtonRef,
    cancelledSumUpOrderId, checkoutKey, submit, placeOrder } = useCheckoutFlow({
      form, cartState, pricing, gls, paymentMethod, setPaymentMethod,
    });

  const previousStep = useRef(step);
  useLayoutEffect(() => {
    if (previousStep.current === step) return;
    previousStep.current = step;
    headingRef.current?.focus({ preventScroll: true });
    window.scrollTo({
      top: 0,
      left: 0,
      behavior: window.matchMedia("(prefers-reduced-motion: reduce)").matches ? "instant" : "smooth",
    });
  }, [step]);

  useEffect(() => {
    if (!cartLoading && draftReady && !deliveryAllowed && method !== FULFILLMENT.PersonalPickup) {
      setDraft(current => ({ ...current, fulfillmentMethod: FULFILLMENT.PersonalPickup, glsDeliveryPointId: null }));
    }
  }, [cartLoading, draftReady, deliveryAllowed, method, setDraft]);

  if (!submitting && (cancelledSumUpOrderId || checkoutKey)) {
    return <CheckoutRecoveryView orderId={cancelledSumUpOrderId} checkoutKey={checkoutKey} busy={submitting} error={error} />;
  }

  if (cart?.items.length && !draftReady && draftStorage.error) {
    return <div className="mx-auto max-w-2xl p-8">
      <p role="alert">{draftStorage.error}</p>
      <button type="button" onClick={draftStorage.load}>Adatok betöltése újra</button>
    </div>;
  }

  if (loading || cartLoading || (cart?.items.length && !draftReady)) {
    return <CheckoutLoadingView />;
  }

  if (!cart?.items.length) {
    return <CheckoutEmptyCartView />;
  }

  return (
    <div className="mx-auto w-full max-w-6xl px-4 py-8">
      <h1 ref={headingRef} tabIndex={-1} className="text-3xl font-bold text-site-heading">Pénztár</h1>
      {draftStorage.error && <div role="alert" className="mt-4 text-sm text-site-danger">
        <p>{draftStorage.error} A megadott adatok ezen az oldalon megmaradtak.</p>
        <button type="button" onClick={draftStorage.retry} className="mr-4 underline">Mentés újrapróbálása</button>
        <button type="button" onClick={draftStorage.load} className="underline">Mentett adatok betöltése (a mostani módosítások elvesznek)</button>
      </div>}
      {error && (
        <div role="alert" className="mt-5 rounded-sm bg-site-danger-soft p-4 text-site-danger">
          {error}
        </div>
      )}

      <form onSubmit={submit} noValidate inert={submitting || leaving} className="mt-7 grid items-start gap-7 lg:grid-cols-[minmax(0,1fr)_22rem]">
        <div className={`order-1 space-y-6 ${step === "payment" ? "hidden" : ""}`}>
          {!authenticated && (
            <fieldset className="rounded-sm border border-site-border bg-white p-6 shadow-md">
              <legend className="px-2 text-xl font-bold">Alapértelmezett rendelési mód</legend>
              <div className="grid gap-3 sm:grid-cols-2">
                <label className="flex cursor-pointer gap-3 rounded-sm border border-site-border p-3 text-sm text-site-text">
                  <input
                    type="radio"
                    name="checkoutCustomerType"
                    checked={draft.customerType === 0}
                    onChange={() => {
                      setDraft((current) => ({
                        ...current,
                        customerType: 0,
                        billingCompanyName: "",
                        billingTaxNumber: "",
                        printedInvoiceRequested: false,
                      }));
                    }}
                    className="mt-1 h-4 w-4 accent-site-green"
                  />
                  <span>
                    <strong className="block text-site-heading">Magánszemély</strong>
                  </span>
                </label>
                <label className="flex cursor-pointer gap-3 rounded-sm border border-site-border p-3 text-sm text-site-text">
                  <input
                    type="radio"
                    name="checkoutCustomerType"
                    checked={draft.customerType === 1}
                    onChange={() => setDraft((current) => ({ ...current, customerType: 1 }))}
                    className="mt-1 h-4 w-4 accent-site-green"
                  />
                  <span>
                    <strong className="block text-site-heading">Cég</strong>
                  </span>
                </label>
              </div>
            </fieldset>
          )}

          <fieldset className="rounded-sm border border-site-border bg-white p-6 shadow-md">
            <legend className="px-2 text-xl font-bold">Átvételi mód</legend>
            <div className="grid gap-3 sm:grid-cols-2">
              {!pricing.methods && !pricing.methodsError && <p role="status">Átvételi módok betöltése...</p>}
              {pricing.methodsError && <div role="alert"><p>{pricing.methodsError}</p><button type="button" onClick={pricing.retry}>Átvételi módok újratöltése</button></div>}
              {pricing.methods?.length === 0 && <p role="alert">Jelenleg nincs elérhető átvételi mód.</p>}
              {pricing.methods?.map(({ fulfillmentMethod: choice, deliveryFee }) => <label key={choice} className={`rounded-sm border p-4 ${choice !== FULFILLMENT.PersonalPickup && !deliveryAllowed ? "opacity-50" : ""}`}>
                <input type="radio" name="fulfillmentMethod" aria-label={FULFILLMENT_LABELS[choice]} checked={method === choice}
                  onChange={() => setMethod(choice)} disabled={choice !== FULFILLMENT.PersonalPickup && !deliveryAllowed} className="mr-3" />
                {FULFILLMENT_LABELS[choice]}
                <span className="mt-1 block text-sm">Szállítási díj: {PRICE.format(deliveryFee)}</span>
              </label>)}
            </div>
          </fieldset>

          {!authenticated && (
            <p className="rounded-sm bg-site-surface-soft p-4 text-sm">
              Felhasználói fiókból történő rendeléshez jelentkezz be itt:{" "}
              <Link to={ROUTES.login} state={{ from: ROUTES.checkout }} onClick={async event => {
                if (event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
                event.preventDefault();
                if (await draftStorage.flush()) navigate(ROUTES.login, { state: { from: ROUTES.checkout } });
              }} className="font-semibold underline">
                Bejelentkezés
              </Link>
            </p>
          )}

          <fieldset className="rounded-sm border border-site-border bg-white p-6 shadow-md">
            <legend className="px-2 text-xl font-bold">Kapcsolattartási adatok</legend>
            {authenticated && (
              <label className="mb-4 flex items-center gap-3 text-sm font-semibold">
                <input
                  type="checkbox"
                  checked={!contactManual}
                  onChange={(event) => handleAccountContactChange(event.target.checked)}
                />
                Fiókban mentett személyes adatok betöltése
              </label>
            )}

            <div className="grid gap-4 sm:grid-cols-2">
              {CONTACT_FIELDS.map(([key, label, autoComplete]) => (
                <label key={key} className="text-sm font-semibold">
                  {label} <span className="text-site-danger">*</span>
                  <input
                    value={draft[key]}
                    onChange={(event) => update(key, event.target.value)}
                    type={key === "customerEmail" ? "email" : key === "customerPhone" ? "tel" : "text"}
                    autoComplete={autoComplete}
                    aria-invalid={Boolean(fieldErrors[key])}
                    className={fieldClass(key)}
                  />
                  {fieldErrors[key] && (
                    <span className="mt-1 block text-xs font-normal text-site-danger">{fieldErrors[key]}</span>
                  )}
                </label>
              ))}
            </div>
          </fieldset>

          {effectiveCustomerType === 0 ? (
            <section className="rounded-sm border border-site-border bg-white p-6 shadow-md">
              <h2 className="text-xl font-bold text-site-heading">Bizonylat</h2>
              <p className="mt-3 text-sm leading-6 text-site-text">
                Magánszemélyként nem kell számlázási címet megadnod. A rendeléshez papíralapú nyugtát mellékelünk.
              </p>
            </section>
          ) : (
          <fieldset className="rounded-sm border border-site-border bg-white p-6 shadow-md">
            <legend className="px-2 text-xl font-bold">Számlázási cím</legend>
            {authenticated && (
              <label className="mb-4 flex items-center gap-3 text-sm font-semibold">
                <input
                  type="checkbox"
                  checked={!billingManual}
                  disabled={!addresses?.billingAddress}
                  onChange={(event) => handleAccountBillingChange(event.target.checked)}
                />
                Fiókban mentett számlázási cím betöltése
              </label>
            )}
            {authenticated && !addresses?.billingAddress && (
              <p className="mb-4 text-sm text-site-danger">
                A fiókban nincs mentett számlázási cím; töltsd ki az alábbi mezőket.
              </p>
            )}

            <div className="grid gap-4 sm:grid-cols-2">
              {([
                ["billingCompanyName", "Cégnév", "organization"],
                ["billingTaxNumber", "Adószám (céges számlához)", "off"],
              ] as const).map(([key, label, autoComplete]) => (
                <label key={key} className="text-sm font-semibold">
                  {label} <span className="text-site-danger">*</span>
                  <input
                    value={draft[key]}
                    onChange={(event) => update(key, event.target.value)}
                    autoComplete={autoComplete}
                    aria-invalid={Boolean(fieldErrors[key])}
                    className={fieldClass(key)}
                  />
                  {fieldErrors[key] && (
                    <span className="mt-1 block text-xs font-normal text-site-danger">{fieldErrors[key]}</span>
                  )}
                </label>
              ))}
            </div>
            <div className="mt-5">
              <AddressFields
                idPrefix="checkout-billing"
                value={billingAddress}
                onChange={handleBillingAddressChange}
                required
                autoCompleteScope="billing"
                invalidFields={{
                  postalCode: Boolean(fieldErrors.postalCode),
                  city: Boolean(fieldErrors.city),
                  street: Boolean(fieldErrors.street),
                  houseNumber: Boolean(fieldErrors.houseNumber),
                }}
                errorMessages={{
                  postalCode: fieldErrors.postalCode,
                  city: fieldErrors.city,
                  street: fieldErrors.street,
                  houseNumber: fieldErrors.houseNumber,
                }}
              />
            </div>
            <label className="mt-4 flex items-start gap-3 rounded-sm bg-site-surface-soft p-3 text-sm text-site-text">
              <input
                type="checkbox"
                checked={draft.printedInvoiceRequested}
                onChange={(event) =>
                  setDraft((current) => ({
                    ...current,
                    printedInvoiceRequested: event.target.checked,
                  }))
                }
                className="mt-1 h-4 w-4 accent-site-green"
              />
              <span>Nyomtatott számlát is kérek. Alapértelmezetten elektronikus számlát küldünk emailben.</span>
            </label>
          </fieldset>
          )}

          {isGlsMethod(method) && <GlsPointPicker key={method} method={method} point={gls.point} loading={gls.loading}
            error={gls.point ? undefined : gls.error ?? fieldErrors.glsDeliveryPointId} onRetry={gls.retry}
            onSelect={point => {
              setDraft(current => ({ ...current, glsDeliveryPointId: point.id, ...emptyDeliveryDraft() }));
              clearFieldErrors(["glsDeliveryPointId"]);
            }} />}

          {method === FULFILLMENT.HomeDelivery && (
            <fieldset className="rounded-sm border border-site-border bg-white p-6 shadow-md">
              <legend className="px-2 text-xl font-bold">
                Szállítási cím
              </legend>
              {authenticated && (
                <label className="mb-4 flex items-center gap-3 text-sm font-semibold">
                  <input
                    type="checkbox"
                    checked={!deliveryManual}
                    disabled={!addresses?.shippingAddress}
                    onChange={(event) => handleAccountDeliveryChange(event.target.checked)}
                  />
                  Fiókban mentett szállítási cím betöltése
                </label>
              )}
              {authenticated && !addresses?.shippingAddress && (
                <p className="mb-4 text-sm text-site-danger">
                  A fiókban nincs mentett szállítási cím; töltsd ki az alábbi mezőket.
                </p>
              )}

              <AddressFields
                idPrefix="checkout-delivery"
                value={deliveryAddress}
                onChange={handleDeliveryAddressChange}
                required
                autoCompleteScope="shipping"
                invalidFields={{
                  postalCode: Boolean(fieldErrors.deliveryPostalCode),
                  city: Boolean(fieldErrors.deliveryCity),
                  street: Boolean(fieldErrors.deliveryStreet),
                  houseNumber: Boolean(fieldErrors.deliveryHouseNumber),
                }}
                errorMessages={{
                  postalCode: fieldErrors.deliveryPostalCode,
                  city: fieldErrors.deliveryCity,
                  street: fieldErrors.deliveryStreet,
                  houseNumber: fieldErrors.deliveryHouseNumber,
                }}
              />
            </fieldset>
          )}
        </div>

        {step === "payment" && (
          <section className="order-3 space-y-6 lg:order-1">
            <fieldset className="rounded-sm border border-site-border bg-white p-6 shadow-md">
              <legend className="px-2 text-xl font-bold">Fizetési mód</legend>
                            <div className="grid gap-3">
                {(Object.keys(PAYMENT_CODES) as PaymentMethod[]).filter(choice => availablePayments.includes(PAYMENT_CODES[choice])
                  && !(choice === "cash-on-delivery" && isGlsMethod(method) && gls.point && !gls.point.acceptsCash && !gls.point.acceptsCard))
                  .map(choice => <label key={choice} className="rounded-sm border p-4">
                    <input type="radio" name="paymentMethod" checked={paymentMethod === choice} onChange={() => setPaymentMethod(choice)} className="mr-3" />
                    <strong>{PAYMENT_LABELS[PAYMENT_CODES[choice]]}</strong>
                    {choice === "cash-on-delivery" && <span className="mt-1 block text-sm text-site-muted">A fizetési díjat az árösszesítés tartalmazza.</span>}
                  </label>)}
              </div>

              {paymentMethod === "online-card" && (
                <div className="mt-5 rounded-sm border border-dashed border-site-green p-5 text-sm">
                  <strong>Biztonságos SumUp fizetés</strong>
                  <p className="mt-1 text-site-muted">
                    A rendelés elküldése után átirányítunk a SumUp fizetési oldalára. Kártyaadatot
                    a saját szerverünk nem kezel.
                  </p>
                </div>
              )}
            </fieldset>
          </section>
        )}

        <CheckoutOrderSummary
          cart={cart}
          quote={quote}
          pricingLoading={pricing.loading}
          pricingError={pricing.methodsError || pricing.error}
          onRetryPricing={pricing.retry}
          method={method}
          paymentMethod={paymentMethod}
          reviewButtonRef={reviewButtonRef}
          className="order-2 lg:order-2"
          step={step}
          submitting={submitting || leaving}
          onBack={async () => {
            if (step === "payment") {
              setStep("details");
              return;
            }
            setLeaving(true);
            try {
              if (await draftStorage.flush()) navigate(ROUTES.cart);
            } finally { setLeaving(false); }
          }}
        />
      </form>

      {showReview && quote && (
        <CheckoutReviewModal
          cart={cart}
          draft={draft}
          effectiveCustomerType={effectiveCustomerType}
          fieldErrors={fieldErrors}
          quote={quote}
          method={method}
          onClose={() => { if (!submitting) setShowReview(false); }}
          deliveryPoint={quote.deliveryPoint}
          onFieldErrorsChange={setFieldErrors}
          onPlaceOrder={() => void placeOrder()}
          onPrivacyAcceptedChange={setPrivacyAccepted}
          onTermsAcceptedChange={setTermsAccepted}
          paymentMethod={paymentMethod}
          privacyAccepted={privacyAccepted}
          reviewModalRef={reviewModalRef}
          submitting={submitting}
          termsAccepted={termsAccepted}
        />
      )}
    </div>
  );
}
