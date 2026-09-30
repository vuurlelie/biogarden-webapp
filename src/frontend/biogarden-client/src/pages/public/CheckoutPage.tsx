import { useEffect, useMemo, useRef, useState, type FormEvent } from "react";
import { Link, useNavigate, useSearchParams } from "react-router";
import { createOrder, createSumUpCheckout } from "../../features/account/api/ordersApi";
import { getAddresses, getProfile } from "../../features/account/api/profileApi";
import type { UserAddresses } from "../../features/account/types/address.types";
import type { UserProfile } from "../../features/account/types/profile.types";
import { useAnalytics } from "../../features/analytics/context/AnalyticsContextCore";
import { getCartValueBucket } from "../../features/analytics/utils/eventValidation";
import { getAuthSession } from "../../features/auth/utils/authStorage";
import { validateCart } from "../../features/cart/api/cartApi";
import { useCart } from "../../features/cart/context/useCart";
import { useCheckoutDraft } from "../../features/checkout/hooks/useCheckoutDraft";
import { useGlsDeliveryPoint } from "../../features/checkout/hooks/useGlsDeliveryPoint";
import { GlsPointPicker } from "../../features/checkout/components/GlsPointPicker";
import { FULFILLMENT, FULFILLMENT_LABELS, isGlsMethod } from "../../features/checkout/constants/fulfillment";
import { CheckoutOrderSummary } from "../../features/checkout/components/CheckoutOrderSummary";
import { CheckoutReviewModal } from "../../features/checkout/components/CheckoutReviewModal";
import {
  CheckoutEmptyCartView,
  CheckoutInterruptedView,
  CheckoutLoadingView,
} from "../../features/checkout/components/CheckoutStatusViews";
import {
  CONTACT_FIELDS,
} from "../../features/checkout/constants/checkoutFields";
import type {
  CheckoutDraft,
  CheckoutDraftTextField,
  CheckoutFieldErrors,
  CheckoutStep,
  FulfillmentMethod,
  PaymentMethod,
} from "../../features/checkout/types/checkout.types";
import {
  billingAddressDraft,
  buildCustomerName,
  deliveryAddressDraft,
  emptyBillingDraft,
  emptyCustomerDraft,
  emptyDeliveryDraft,
} from "../../features/checkout/utils/checkoutDraft";
import { PRICE } from "../../features/checkout/utils/checkoutFormatting";
import { buildCheckoutOrderRequest } from "../../features/checkout/utils/checkoutOrderPayload";
import {
  isCheckoutFieldValid,
  validateCheckoutFields,
} from "../../features/checkout/utils/checkoutValidation";
import { ROUTES } from "../../shared/constants/routes";
import AddressFields from "../../shared/components/forms/AddressFields";
import { useModalFocus } from "../../shared/hooks/useModalFocus";
import type { AddressFormData } from "../../shared/types/address.types";
import { getErrorMessage, getServerFieldErrors, hasErrorStatus } from "../../shared/utils/errorHandling";

export default function CheckoutPage() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const { cart, isLoading: cartLoading, refresh } = useCart();
  const authenticated = Boolean(getAuthSession());
  const [addresses, setAddresses] = useState<UserAddresses | null>(null);
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [contactManual, setContactManual] = useState(!authenticated);
  const [billingManual, setBillingManual] = useState(!authenticated);
  const [deliveryManual, setDeliveryManual] = useState(!authenticated);
  const draftStorage = useCheckoutDraft(cart?.items.length ? cart.id : undefined);
  const { draft, setDraft, ready: draftReady } = draftStorage;
  const method = draft.fulfillmentMethod;
  const gls = useGlsDeliveryPoint(draft.glsDeliveryPointId, method);
  const [loading, setLoading] = useState(authenticated);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [fieldErrors, setFieldErrors] = useState<CheckoutFieldErrors>({});
  const [showReview, setShowReview] = useState(false);
  const [termsAccepted, setTermsAccepted] = useState(false);
  const [privacyAccepted, setPrivacyAccepted] = useState(false);
  const [step, setStep] = useState<CheckoutStep>("details");
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod | null>(null);
  const reviewModalRef = useRef<HTMLElement | null>(null);
  const reviewButtonRef = useRef<HTMLButtonElement | null>(null);
  const checkoutStartedTrackedRef = useRef(false);
  const { track } = useAnalytics();
  const hasDraft = Object.values(draft).some(Boolean);
  const deliveryAllowed = useMemo(
    () => Boolean(cart?.items.length && cart.items.every((item) => item.deliveryAvailable)),
    [cart],
  );
  const deliveryFee = method === 1 ? 1500 : method === 2 ? 1000 : 0;
  const paymentFee = paymentMethod === "cash-on-delivery" ? 500 : 0;
  const grandTotal = (cart?.totalPrice ?? 0) + deliveryFee + paymentFee;
  const effectiveCustomerType = authenticated && profile ? profile.customerType : draft.customerType;
  const billingAddress: AddressFormData = {
    postalCode: draft.postalCode,
    city: draft.city,
    street: draft.street,
    houseNumber: draft.houseNumber,
    building: draft.building,
    floor: draft.floor,
    door: draft.door,
    doorbell: draft.doorbell,
  };
  const deliveryAddress: AddressFormData = {
    postalCode: draft.deliveryPostalCode,
    city: draft.deliveryCity,
    street: draft.deliveryStreet,
    houseNumber: draft.deliveryHouseNumber,
    building: draft.deliveryBuilding,
    floor: draft.deliveryFloor,
    door: draft.deliveryDoor,
    doorbell: draft.deliveryDoorbell,
  };
  const cancelledSumUpOrderId = searchParams.get("sumup") === "cancelled"
    ? searchParams.get("orderId")
    : null;

  useModalFocus(reviewModalRef, {
    isOpen: showReview && !cancelledSumUpOrderId && !loading && !cartLoading && draftReady && Boolean(cart?.items.length),
    onClose: () => { if (!submitting) setShowReview(false); },
    returnFocusRef: reviewButtonRef,
  });

  useEffect(() => {
    if (!authenticated || profile) {
      setLoading(false);
      return;
    }
    if (!draftReady) return;

    let ignore = false;

    void Promise.all([getAddresses(), getProfile()])
      .then(([loadedAddresses, loadedProfile]) => {
        if (ignore) return;

        setAddresses(loadedAddresses);
        setProfile(loadedProfile);

        if (!hasDraft) {
          setContactManual(false);
          setBillingManual(!loadedAddresses.billingAddress);
          setDeliveryManual(!loadedAddresses.shippingAddress);
          setDraft((current) => ({
            ...current,
            customerName: buildCustomerName(loadedProfile),
            customerEmail: loadedProfile.email ?? "",
            customerPhone: loadedProfile.phoneNumber ?? "",
            customerType: loadedProfile.customerType,
            billingCompanyName: loadedProfile.companyName ?? "",
            billingTaxNumber: loadedProfile.taxNumber ?? "",
            printedInvoiceRequested: loadedProfile.printedInvoiceRequested,
            ...(loadedAddresses.billingAddress ? billingAddressDraft(loadedAddresses.billingAddress) : {}),
            ...(loadedAddresses.shippingAddress ? deliveryAddressDraft(loadedAddresses.shippingAddress) : {}),
          }));
        } else {
          // Restored values are a checkout snapshot, not a live selection of profile data.
          setContactManual(true);
          setBillingManual(true);
          setDeliveryManual(true);
        }
      })
      .catch((loadError) => {
        if (!ignore) {
          if (hasErrorStatus(loadError, 401)) {
            navigate(ROUTES.login, { replace: true });
            return;
          }
          setError(getErrorMessage(loadError, "A fiókadatok betöltése nem sikerült."));
        }
      })
      .finally(() => {
        if (!ignore) setLoading(false);
      });

    return () => {
      ignore = true;
    };
  }, [authenticated, hasDraft, navigate, profile, draftReady, setDraft]);

  useEffect(() => {
    if (!cartLoading && draftReady && !deliveryAllowed && method !== FULFILLMENT.PersonalPickup) {
      setDraft(current => ({ ...current, fulfillmentMethod: FULFILLMENT.PersonalPickup, glsDeliveryPointId: null }));
    }
  }, [cartLoading, draftReady, deliveryAllowed, method, setDraft]);

  useEffect(() => {
    setPaymentMethod(null);
    setShowReview(false);
    setTermsAccepted(false);
    setPrivacyAccepted(false);
  }, [method, draft.glsDeliveryPointId]);

  function setMethod(next: FulfillmentMethod) {
    setDraft(current => ({ ...current, fulfillmentMethod: next, glsDeliveryPointId: null,
      ...(isGlsMethod(next) ? emptyDeliveryDraft() : {}) }));
    setFieldErrors({});
  }

  useEffect(() => {
    if (!cart?.items.length || checkoutStartedTrackedRef.current) return;

    checkoutStartedTrackedRef.current = true;
    track("checkout_started", {
      itemCount: cart.totalItemCount,
      cartValueBucket: getCartValueBucket(cart.totalPrice),
    });
  }, [cart, track]);

  function update(field: CheckoutDraftTextField, value: string) {
    setDraft((current) => ({ ...current, [field]: value }));
    setFieldErrors((current) => ({ ...current, [field]: undefined }));
  }

  function fieldClass(field: CheckoutDraftTextField): string {
    const base = "mt-2 block w-full rounded-sm border px-3 py-2.5 disabled:cursor-default";
    if (fieldErrors[field]) return `${base} border-site-danger bg-white`;

    return isCheckoutFieldValid(field, draft)
      ? `${base} border-site-green bg-site-surface-soft`
      : `${base} border-site-border bg-white`;
  }

  function clearFieldErrors(fields: Array<keyof CheckoutFieldErrors>) {
    setFieldErrors((current) => {
      const next = { ...current };
      fields.forEach((field) => {
        next[field] = undefined;
      });
      return next;
    });
  }

  function handleBillingAddressChange(address: AddressFormData) {
    clearFieldErrors(["postalCode", "city", "street", "houseNumber", "billingAddress"]);
    setDraft((current) => ({
      ...current,
      postalCode: address.postalCode,
      city: address.city,
      street: address.street,
      houseNumber: address.houseNumber,
      building: address.building ?? "",
      floor: address.floor ?? "",
      door: address.door ?? "",
      doorbell: address.doorbell ?? "",
    }));
  }

  function handleDeliveryAddressChange(address: AddressFormData) {
    clearFieldErrors([
      "deliveryPostalCode",
      "deliveryCity",
      "deliveryStreet",
      "deliveryHouseNumber",
      "deliveryAddress",
    ]);
    setDraft((current) => ({
      ...current,
      deliveryPostalCode: address.postalCode,
      deliveryCity: address.city,
      deliveryStreet: address.street,
      deliveryHouseNumber: address.houseNumber,
      deliveryBuilding: address.building ?? "",
      deliveryFloor: address.floor ?? "",
      deliveryDoor: address.door ?? "",
      deliveryDoorbell: address.doorbell ?? "",
    }));
  }

  function handleAccountContactChange(checked: boolean) {
    setContactManual(!checked);
    clearFieldErrors(["customerName", "customerEmail", "customerPhone", "accountContact"]);
    setDraft((current) => (
      checked && profile
        ? {
            ...current,
            customerName: buildCustomerName(profile),
            customerEmail: profile.email ?? "",
            customerPhone: profile.phoneNumber ?? "",
          }
        : { ...current, ...emptyCustomerDraft() }
    ));
  }

  function handleAccountBillingChange(checked: boolean) {
    setBillingManual(!checked);
    clearFieldErrors([
      "billingCompanyName",
      "billingTaxNumber",
      "postalCode",
      "city",
      "street",
      "houseNumber",
      "billingAddress",
    ]);

    const address = addresses?.billingAddress;
    setDraft((current) => (
      checked && address
        ? {
            ...current,
            billingCompanyName: profile?.companyName ?? "",
            billingTaxNumber: profile?.taxNumber ?? "",
            ...billingAddressDraft(address),
          }
        : { ...current, ...emptyBillingDraft() }
    ));
  }

  function handleAccountDeliveryChange(checked: boolean) {
    setDeliveryManual(!checked);
    clearFieldErrors([
      "deliveryPostalCode",
      "deliveryCity",
      "deliveryStreet",
      "deliveryHouseNumber",
      "deliveryAddress",
    ]);

    const address = addresses?.shippingAddress;
    setDraft((current) => (
      checked && address
        ? { ...current, ...deliveryAddressDraft(address) }
        : { ...current, ...emptyDeliveryDraft() }
    ));
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    setError("");

    if (!cart?.items.length) {
      setError("A kosár üres.");
      return;
    }

    const errors = validateCheckoutFields(draft, effectiveCustomerType, method);
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
    if (!await draftStorage.discard()) return;
    setContactManual(true);
    setBillingManual(true);
    setDeliveryManual(true);
    setTermsAccepted(false);
    setPrivacyAccepted(false);
    setFieldErrors({});
    setStep("details");
  }

  async function placeOrder() {
    if (submitting) return;
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
    try {
      await draftStorage.pause();
      const order = await createOrder(buildCheckoutOrderRequest(draft, effectiveCustomerType, method, cart.id));
      orderCreated = true;

      track("order_submitted", {
        paymentMethod,
        fulfillmentMethod: String(method),
        itemCount: cart.totalItemCount,
        cartValueBucket: getCartValueBucket(cart.totalPrice),
      });

      if (paymentMethod === "online-card") {
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
      if (!orderCreated) draftStorage.resume();
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

  async function retrySumUpPayment(orderId: string) {
    setSubmitting(true);
    setError("");

    try {
      const checkout = await createSumUpCheckout(orderId);
      window.location.assign(checkout.url);
    } catch (retryError) {
      setError(getErrorMessage(retryError, "A fizetés újraindítása nem sikerült."));
    } finally {
      setSubmitting(false);
    }
  }

  if (cancelledSumUpOrderId) {
    return (
      <CheckoutInterruptedView
        error={error}
        submitting={submitting}
        onRetry={() => void retrySumUpPayment(cancelledSumUpOrderId)}
      />
    );
  }

  if (cart?.items.length && !draftReady && draftStorage.error) {
    return <main className="mx-auto max-w-2xl p-8">
      <p role="alert">{draftStorage.error}</p>
      <button type="button" onClick={draftStorage.load}>Piszkozat betöltése újra</button>
    </main>;
  }

  if (loading || cartLoading || (cart?.items.length && !draftReady)) {
    return <CheckoutLoadingView />;
  }

  if (!cart?.items.length) {
    return <CheckoutEmptyCartView />;
  }

  return (
    <main className="mx-auto w-full max-w-6xl px-4 py-8">
      <h1 className="text-3xl font-bold text-site-heading">Pénztár</h1>
      <div className="my-4 text-sm text-site-muted">
        <p>A piszkozat legfeljebb 30 percig, a kosár lejártáig állítható vissza. A mentés nem hosszabbítja meg a készletfoglalást.</p>
        <p role="status">{draftStorage.saving ? "Piszkozat mentése..." : "Az adatok automatikus mentése bekapcsolva."}</p>
        <button type="button" disabled={submitting} onClick={() => void discardDraft()} className="mt-2 underline">Piszkozat törlése</button>
        {draftStorage.error && <div role="alert" className="mt-2 text-site-danger">
          <p>{draftStorage.error} A módosításaid az oldalon megmaradtak.</p>
          <button type="button" onClick={draftStorage.retry} className="mr-4 underline">Mentés újrapróbálása</button>
          <button type="button" onClick={draftStorage.load} className="underline">Mentett változat betöltése (helyi módosítások elvetése)</button>
        </div>}
      </div>
      {error && (
        <div role="alert" className="mt-5 rounded-sm bg-site-danger-soft p-4 text-site-danger">
          {error}
        </div>
      )}

      <form onSubmit={submit} className="mt-7 grid items-start gap-7 lg:grid-cols-[minmax(0,1fr)_22rem]">
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
              {Object.values(FULFILLMENT).map(choice => <label key={choice} className={`rounded-sm border p-4 ${choice !== FULFILLMENT.PersonalPickup && !deliveryAllowed ? "opacity-50" : ""}`}>
                <input type="radio" name="fulfillmentMethod" checked={method === choice}
                  onChange={() => setMethod(choice)} disabled={choice !== FULFILLMENT.PersonalPickup && !deliveryAllowed} className="mr-3" />
                {FULFILLMENT_LABELS[choice]}
              </label>)}
            </div>
          </fieldset>

          {!authenticated && (
            <p className="rounded-sm bg-site-surface-soft p-4 text-sm">
              Felhasználói fiókból történő rendeléshez jelentkezz be itt:{" "}
              <Link to={ROUTES.login} state={{ from: ROUTES.checkout }} className="font-semibold underline">
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
                    <span className="mt-1 block text-xs text-site-danger">{fieldErrors[key]}</span>
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
                Fiókban mentett számlázási cím használata
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
                    disabled={!billingManual}
                    onChange={(event) => update(key, event.target.value)}
                    autoComplete={autoComplete}
                    aria-invalid={Boolean(fieldErrors[key])}
                    className={fieldClass(key)}
                  />
                  {fieldErrors[key] && (
                    <span className="mt-1 block text-xs text-site-danger">{fieldErrors[key]}</span>
                  )}
                </label>
              ))}
            </div>
            <div className="mt-5">
              <AddressFields
                idPrefix="checkout-billing"
                value={billingAddress}
                onChange={handleBillingAddressChange}
                disabled={!billingManual}
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
                  Fiókban mentett szállítási cím használata
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
                disabled={!deliveryManual}
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
            <button
              type="button"
              onClick={() => setStep("details")}
              className="text-sm font-semibold text-site-green-dark underline"
            >
              ← Vissza az adatokhoz
            </button>

            <fieldset className="rounded-sm border border-site-border bg-white p-6 shadow-md">
              <legend className="px-2 text-xl font-bold">Fizetési mód</legend>
              <div className="grid gap-3">
                <label className="rounded-sm border p-4">
                  <input
                    type="radio"
                    checked={paymentMethod === "online-card"}
                    onChange={() => setPaymentMethod("online-card")}
                    className="mr-3"
                  />
                  <strong>Online bankkártyás fizetés</strong>
                  <span className="mt-1 block text-sm text-site-muted">
                    Biztonságos online kártyás fizetés SumUp-on keresztül.
                  </span>
                </label>

                {method === 0 ? (
                  <label className="rounded-sm border p-4">
                    <input
                      type="radio"
                      checked={paymentMethod === "pay-at-pickup"}
                      onChange={() => setPaymentMethod("pay-at-pickup")}
                      className="mr-3"
                    />
                    <strong>Fizetés átvételkor</strong>
                    <span className="mt-1 block text-sm text-site-muted">
                      A kertészetben készpénzzel vagy bankkártyával.
                    </span>
                  </label>
                ) : (
                  <label className="rounded-sm border p-4">
                    <input
                      type="radio"
                      checked={paymentMethod === "cash-on-delivery"}
                      onChange={() => setPaymentMethod("cash-on-delivery")}
                      className="mr-3"
                    />
                    <strong>Utánvét</strong>
                    <span className="mt-1 block text-sm text-site-muted">
                      Teszt utánvételi díj: {PRICE.format(500)}.
                    </span>
                  </label>
                )}
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
          deliveryFee={deliveryFee}
          grandTotal={grandTotal}
          method={method}
          paymentFee={paymentFee}
          paymentMethod={paymentMethod}
          reviewButtonRef={reviewButtonRef}
          className="order-2 lg:order-2"
          step={step}
          submitting={submitting}
        />
      </form>

      {showReview && (
        <CheckoutReviewModal
          cart={cart}
          draft={draft}
          effectiveCustomerType={effectiveCustomerType}
          fieldErrors={fieldErrors}
          grandTotal={grandTotal}
          method={method}
          onClose={() => { if (!submitting) setShowReview(false); }}
          deliveryPoint={gls.point}
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
    </main>
  );
}
