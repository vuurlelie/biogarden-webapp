import { useEffect, useMemo, useState, type FormEvent } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { createOrder, createSumUpCheckout } from "../../features/account/api/ordersApi";
import { getAddresses, getProfile } from "../../features/account/api/profileApi";
import type { UserAddresses } from "../../features/account/types/address.types";
import type { UserProfile } from "../../features/account/types/profile.types";
import { getAuthSession } from "../../features/auth/utils/authStorage";
import { validateCart } from "../../features/cart/api/cartApi";
import { useCart } from "../../features/cart/context/useCart";
import type { CartItem } from "../../features/cart/types/cart.types";
import { getUnitLabel } from "../../features/products/utils/productLabels";
import { LEGAL_DOCUMENT_VERSIONS } from "../../shared/constants/legalDocuments";
import { ROUTES } from "../../shared/constants/routes";

const DRAFT_KEY = "biogarden_checkout_draft";
const IDEMPOTENCY_KEY = "biogarden_checkout_idempotency_key";
const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;
const PRICE = new Intl.NumberFormat("hu-HU", {
  style: "currency",
  currency: "HUF",
  maximumFractionDigits: 0,
});

type FulfillmentMethod = 0 | 1 | 2;
type PaymentMethod = "online-card" | "pay-at-pickup" | "cash-on-delivery";
type CheckoutStep = "details" | "payment";

type Draft = {
  customerName: string;
  customerEmail: string;
  customerPhone: string;
  postalCode: string;
  city: string;
  street: string;
  houseNumber: string;
  building: string;
  floor: string;
  door: string;
  doorbell: string;
  customerType: 0 | 1;
  billingCompanyName: string;
  billingTaxNumber: string;
  printedInvoiceRequested: boolean;
  deliveryPostalCode: string;
  deliveryCity: string;
  deliveryStreet: string;
  deliveryHouseNumber: string;
  deliveryBuilding: string;
  deliveryFloor: string;
  deliveryDoor: string;
  deliveryDoorbell: string;
};

type FieldErrors = Partial<
  Record<
    keyof Draft | "accountContact" | "billingAddress" | "deliveryAddress" | "terms" | "privacy",
    string
  >
>;

type DraftTextField = {
  [Key in keyof Draft]: Draft[Key] extends string ? Key : never;
}[keyof Draft];

type FieldDefinition = [DraftTextField, string, string];

type SummaryCartItemProps = {
  item: CartItem;
  compact?: boolean;
};

const EMPTY_DRAFT: Draft = {
  customerName: "",
  customerEmail: "",
  customerPhone: "",
  postalCode: "",
  city: "",
  street: "",
  houseNumber: "",
  building: "",
  floor: "",
  door: "",
  doorbell: "",
  customerType: 0,
  billingCompanyName: "",
  billingTaxNumber: "",
  printedInvoiceRequested: false,
  deliveryPostalCode: "",
  deliveryCity: "",
  deliveryStreet: "",
  deliveryHouseNumber: "",
  deliveryBuilding: "",
  deliveryFloor: "",
  deliveryDoor: "",
  deliveryDoorbell: "",
};

const CONTACT_FIELDS: FieldDefinition[] = [
  ["customerName", "Név", "name"],
  ["customerEmail", "Email cím", "email"],
  ["customerPhone", "Telefonszám", "tel"],
];

const BILLING_FIELDS: FieldDefinition[] = [
  ["billingCompanyName", "Cégnév", "organization"],
  ["billingTaxNumber", "Adószám (céges számlához)", "off"],
  ["postalCode", "Irányítószám", "postal-code"],
  ["city", "Település", "address-level2"],
  ["street", "Közterület neve és jellege", "address-line1"],
  ["houseNumber", "Házszám", "address-line2"],
  ["building", "Épület", "off"],
  ["floor", "Emelet", "off"],
  ["door", "Ajtó", "off"],
  ["doorbell", "Kapucsengő", "off"],
];

const DELIVERY_FIELDS: FieldDefinition[] = [
  ["deliveryPostalCode", "Irányítószám", "shipping postal-code"],
  ["deliveryCity", "Település", "shipping address-level2"],
  ["deliveryStreet", "Közterület neve és jellege", "shipping address-line1"],
  ["deliveryHouseNumber", "Házszám", "shipping address-line2"],
  ["deliveryBuilding", "Épület", "off"],
  ["deliveryFloor", "Emelet", "off"],
  ["deliveryDoor", "Ajtó", "off"],
  ["deliveryDoorbell", "Kapucsengő", "off"],
];

const REQUIRED_BILLING_FIELDS: Array<keyof Draft> = [
  "billingCompanyName",
  "billingTaxNumber",
  "postalCode",
  "city",
  "street",
  "houseNumber",
];

const REQUIRED_DELIVERY_FIELDS: Array<keyof Draft> = [
  "deliveryPostalCode",
  "deliveryCity",
  "deliveryStreet",
  "deliveryHouseNumber",
];

function loadDraft(): Draft {
  try {
    return { ...EMPTY_DRAFT, ...JSON.parse(sessionStorage.getItem(DRAFT_KEY) ?? "{}") };
  } catch {
    return EMPTY_DRAFT;
  }
}

function getOrCreateCheckoutIdempotencyKey(): string {
  const existing = sessionStorage.getItem(IDEMPOTENCY_KEY);
  if (existing) return existing;

  const generated = crypto.randomUUID().replaceAll("-", "");
  sessionStorage.setItem(IDEMPOTENCY_KEY, generated);
  return generated;
}

function buildCustomerName(profile: UserProfile): string {
  return [profile.lastName, profile.firstName].filter(Boolean).join(" ");
}

function billingAddressDraft(address: NonNullable<UserAddresses["billingAddress"]>): Partial<Draft> {
  return {
    postalCode: address.postalCode,
    city: address.city,
    street: address.street,
    houseNumber: address.houseNumber,
    building: address.building ?? "",
    floor: address.floor ?? "",
    door: address.door ?? "",
    doorbell: address.doorbell ?? "",
  };
}

function deliveryAddressDraft(address: NonNullable<UserAddresses["shippingAddress"]>): Partial<Draft> {
  return {
    deliveryPostalCode: address.postalCode,
    deliveryCity: address.city,
    deliveryStreet: address.street,
    deliveryHouseNumber: address.houseNumber,
    deliveryBuilding: address.building ?? "",
    deliveryFloor: address.floor ?? "",
    deliveryDoor: address.door ?? "",
    deliveryDoorbell: address.doorbell ?? "",
  };
}

function emptyCustomerDraft(): Partial<Draft> {
  return {
    customerName: "",
    customerEmail: "",
    customerPhone: "",
  };
}

function emptyBillingDraft(): Partial<Draft> {
  return {
    billingCompanyName: "",
    billingTaxNumber: "",
    postalCode: "",
    city: "",
    street: "",
    houseNumber: "",
    building: "",
    floor: "",
    door: "",
    doorbell: "",
  };
}

function emptyDeliveryDraft(): Partial<Draft> {
  return {
    deliveryPostalCode: "",
    deliveryCity: "",
    deliveryStreet: "",
    deliveryHouseNumber: "",
    deliveryBuilding: "",
    deliveryFloor: "",
    deliveryDoor: "",
    deliveryDoorbell: "",
  };
}

function paymentLabel(paymentMethod: PaymentMethod | null): string {
  if (paymentMethod === "pay-at-pickup") return "Fizetés átvételkor";
  if (paymentMethod === "cash-on-delivery") return "Utánvét";
  return "Online bankkártya";
}

function getCartImageUrl(image: string | null): string | null {
  return image ? `${API_BASE_URL}/${image.replace(/^\/+/, "")}` : null;
}

function SummaryCartItem({ item, compact = false }: SummaryCartItemProps) {
  const imageUrl = getCartImageUrl(item.image);
  const description = item.productDescription?.trim();

  return (
    <li className="grid grid-cols-[4.25rem_minmax(0,1fr)_auto] gap-3 py-3 text-sm first:pt-0 last:pb-0">
      <div className="relative h-16 w-16 shrink-0 overflow-visible">
        <div className="h-full w-full overflow-hidden rounded-sm bg-white">
          {imageUrl ? (
            <img src={imageUrl} alt={item.productName} className="h-full w-full object-cover" />
          ) : (
            <div className="flex h-full w-full items-center justify-center text-[0.65rem] text-site-muted">
              Nincs kép
            </div>
          )}
        </div>
        <span className="absolute -right-2 -top-2 flex h-7 min-w-7 items-center justify-center rounded-full border border-site-border bg-white px-2 text-sm font-bold text-site-heading shadow-sm">
          {item.quantity}
        </span>
      </div>

      <div className="min-w-0">
        <h3 className="font-bold leading-snug text-site-heading">{item.productName}</h3>
        <p className="mt-1 text-site-text">
          {PRICE.format(item.unitPrice)} / {getUnitLabel(item.unit)}
        </p>
        {description && !compact && (
          <p className="mt-1 max-h-10 overflow-hidden text-site-muted">{description}</p>
        )}
      </div>

      <strong className="whitespace-nowrap text-right text-site-heading">{PRICE.format(item.lineTotal)}</strong>
    </li>
  );
}

export default function CheckoutPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { cart, isLoading: cartLoading, refresh } = useCart();
  const authenticated = Boolean(getAuthSession());
  const [addresses, setAddresses] = useState<UserAddresses | null>(null);
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [contactManual, setContactManual] = useState(!authenticated);
  const [billingManual, setBillingManual] = useState(!authenticated);
  const [deliveryManual, setDeliveryManual] = useState(!authenticated);
  const [draft, setDraft] = useState<Draft>(loadDraft);
  const [method, setMethod] = useState<FulfillmentMethod>(0);
  const [loading, setLoading] = useState(authenticated);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [showReview, setShowReview] = useState(false);
  const [termsAccepted, setTermsAccepted] = useState(false);
  const [privacyAccepted, setPrivacyAccepted] = useState(false);
  const [step, setStep] = useState<CheckoutStep>("details");
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod | null>(null);
  const hasDraft = Object.values(draft).some(Boolean);
  const deliveryAllowed = useMemo(
    () => Boolean(cart?.items.length && cart.items.every((item) => item.deliveryAvailable)),
    [cart],
  );
  const deliveryFee = method === 1 ? 1500 : method === 2 ? 1000 : 0;
  const paymentFee = paymentMethod === "cash-on-delivery" ? 500 : 0;
  const grandTotal = (cart?.totalPrice ?? 0) + deliveryFee + paymentFee;
  const effectiveCustomerType = authenticated && profile ? profile.customerType : draft.customerType;
  const cancelledSumUpOrderId = searchParams.get("sumup") === "cancelled"
    ? searchParams.get("orderId")
    : null;

  useEffect(() => {
    sessionStorage.setItem(DRAFT_KEY, JSON.stringify(draft));
  }, [draft]);

  useEffect(() => {
    if (!authenticated) {
      setLoading(false);
      return;
    }

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
        }
      })
      .catch((loadError) => {
        if (!ignore) {
          setError(loadError instanceof Error ? loadError.message : "A fiókadatok betöltése nem sikerült.");
        }
      })
      .finally(() => {
        if (!ignore) setLoading(false);
      });

    return () => {
      ignore = true;
    };
  }, [authenticated, hasDraft]);

  useEffect(() => {
    if (!deliveryAllowed && method !== 0) setMethod(0);
  }, [deliveryAllowed, method]);

  useEffect(() => {
    setPaymentMethod(null);
  }, [method]);

  function update(field: DraftTextField, value: string) {
    setDraft((current) => ({ ...current, [field]: value }));
    setFieldErrors((current) => ({ ...current, [field]: undefined }));
  }

  function fieldClass(field: DraftTextField): string {
    const base = "mt-2 block w-full rounded-sm border px-3 py-2.5 disabled:cursor-default";
    if (fieldErrors[field]) return `${base} border-site-danger bg-white`;

    const value = draft[field].trim();
    const valid =
      field === "customerName" ? value.length >= 2
        : field === "customerEmail" ? /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value)
        : field === "customerPhone" ? value.replace(/\D/g, "").length >= 9
        : field === "postalCode" || field === "deliveryPostalCode" ? /^\d{4}$/.test(value)
        : field === "city" || field === "street" || field === "deliveryCity" || field === "deliveryStreet"
          ? value.length >= 2
        : field === "houseNumber" || field === "deliveryHouseNumber" ? value.length > 0
        : field === "billingCompanyName"
          ? value.length > 0 && /^\d{8}(-\d-\d{2})?$/.test(draft.billingTaxNumber.trim())
        : field === "billingTaxNumber"
          ? /^\d{8}(-\d-\d{2})?$/.test(value) && draft.billingCompanyName.trim().length > 0
        : value.length > 0;

    return valid ? `${base} border-site-green bg-site-surface-soft` : `${base} border-site-border bg-white`;
  }

  function clearFieldErrors(fields: Array<keyof FieldErrors>) {
    setFieldErrors((current) => {
      const next = { ...current };
      fields.forEach((field) => {
        next[field] = undefined;
      });
      return next;
    });
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

  function validateFields(): FieldErrors {
    const errors: FieldErrors = {};

    if (draft.customerName.trim().length < 2) {
      errors.customerName = "A név megadása kötelező, legalább 2 karakterrel.";
    }
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(draft.customerEmail.trim())) {
      errors.customerEmail = "Adj meg egy érvényes email címet.";
    }
    if (draft.customerPhone.replace(/\D/g, "").length < 9) {
      errors.customerPhone = "Adj meg egy érvényes telefonszámot.";
    }
    if (effectiveCustomerType === 1) {
    if (!/^\d{4}$/.test(draft.postalCode.trim())) {
      errors.postalCode = "Az irányítószám 4 számjegyből álljon.";
    }
    if (draft.city.trim().length < 2) {
      errors.city = "A település megadása kötelező.";
    }
    if (draft.street.trim().length < 2) {
      errors.street = "A közterület megadása kötelező.";
    }
    if (!draft.houseNumber.trim()) {
      errors.houseNumber = "A házszám megadása kötelező.";
    }

    const hasCompanyName = Boolean(draft.billingCompanyName.trim());
    const hasTaxNumber = Boolean(draft.billingTaxNumber.trim());
    if (hasCompanyName !== hasTaxNumber) {
      errors.billingCompanyName = "Céges számlázáshoz a cégnév és az adószám együtt szükséges.";
      errors.billingTaxNumber = "Céges számlázáshoz a cégnév és az adószám együtt szükséges.";
    } else if (hasTaxNumber && !/^\d{8}(-\d-\d{2})?$/.test(draft.billingTaxNumber.trim())) {
      errors.billingTaxNumber = "Hibás adószám. Példa: 12345678-1-12.";
    }

    }

    if (method !== 0) {
      if (!/^\d{4}$/.test(draft.deliveryPostalCode.trim())) {
        errors.deliveryPostalCode = "Az irányítószám 4 számjegyből álljon.";
      }
      if (draft.deliveryCity.trim().length < 2) {
        errors.deliveryCity = "A település megadása kötelező.";
      }
      if (draft.deliveryStreet.trim().length < 2) {
        errors.deliveryStreet = "A közterület megadása kötelező.";
      }
      if (!draft.deliveryHouseNumber.trim()) {
        errors.deliveryHouseNumber = "A házszám megadása kötelező.";
      }
    }

    return errors;
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    setError("");

    if (!cart?.items.length) {
      setError("A kosár üres.");
      return;
    }

    const errors = validateFields();
    setFieldErrors(errors);
    if (Object.keys(errors).length) {
      setError("Ellenőrizd a pirossal jelölt mezőket.");
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
      } else {
        setShowReview(true);
      }
    } catch (validationError) {
      setError(validationError instanceof Error ? validationError.message : "A kosár ellenőrzése nem sikerült.");
    } finally {
      setSubmitting(false);
    }
  }

  async function placeOrder() {
    const consentErrors: FieldErrors = {};
    if (!termsAccepted) {
      consentErrors.terms = "Az Általános Szerződési Feltételek elfogadása kötelező.";
    }
    if (!privacyAccepted) {
      consentErrors.privacy = "Az Adatkezelési Tájékoztató elfogadása kötelező.";
    }

    setFieldErrors((current) => ({ ...current, ...consentErrors }));
    if (Object.keys(consentErrors).length) return;

    setSubmitting(true);
    setError("");

    try {
      const order = await createOrder({
        idempotencyKey: getOrCreateCheckoutIdempotencyKey(),
        fulfillmentMethod: method,
        customerName: draft.customerName,
        customerEmail: draft.customerEmail,
        customerPhone: draft.customerPhone,
        customerType: effectiveCustomerType,
        addressId: null,
        postalCode: effectiveCustomerType === 1 ? draft.postalCode : "",
        city: effectiveCustomerType === 1 ? draft.city : "",
        street: effectiveCustomerType === 1 ? draft.street : "",
        houseNumber: effectiveCustomerType === 1 ? draft.houseNumber : "",
        building: effectiveCustomerType === 1 ? draft.building : "",
        floor: effectiveCustomerType === 1 ? draft.floor : "",
        door: effectiveCustomerType === 1 ? draft.door : "",
        doorbell: effectiveCustomerType === 1 ? draft.doorbell : "",
        billingCompanyName: effectiveCustomerType === 1 ? draft.billingCompanyName : "",
        billingTaxNumber: effectiveCustomerType === 1 ? draft.billingTaxNumber : "",
        printedInvoiceRequested: effectiveCustomerType === 1 && draft.printedInvoiceRequested,
        acceptedTermsVersion: LEGAL_DOCUMENT_VERSIONS.terms,
        acceptedPrivacyPolicyVersion: LEGAL_DOCUMENT_VERSIONS.privacyPolicy,
        deliveryAddressId: null,
        ...(method !== 0
          ? {
              deliveryPostalCode: draft.deliveryPostalCode,
              deliveryCity: draft.deliveryCity,
              deliveryStreet: draft.deliveryStreet,
              deliveryHouseNumber: draft.deliveryHouseNumber,
              deliveryBuilding: draft.deliveryBuilding,
              deliveryFloor: draft.deliveryFloor,
              deliveryDoor: draft.deliveryDoor,
              deliveryDoorbell: draft.deliveryDoorbell,
            }
          : {}),
      });

      sessionStorage.removeItem(DRAFT_KEY);
      sessionStorage.removeItem(IDEMPOTENCY_KEY);

      if (paymentMethod === "online-card") {
        const checkout = await createSumUpCheckout(order.id);
        window.location.assign(checkout.url);
        return;
      }

      await refresh();
      navigate(ROUTES.orderSuccess, { replace: true, state: { order } });
    } catch (submitError) {
      setShowReview(false);
      setError(submitError instanceof Error ? submitError.message : "A rendelést nem sikerült elküldeni.");
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
      setError(retryError instanceof Error ? retryError.message : "A fizetés újraindítása nem sikerült.");
    } finally {
      setSubmitting(false);
    }
  }

  if (loading || cartLoading) {
    return (
      <div className="mx-auto max-w-7xl px-4 py-12 text-center">
        A pénztár betöltése...
      </div>
    );
  }

  if (!cart?.items.length && cancelledSumUpOrderId) {
    return (
      <main className="mx-auto max-w-2xl px-4 py-16 text-center">
        <h1 className="text-3xl font-bold text-site-heading">A fizetés megszakadt</h1>
        <p className="mt-4 text-site-text">
          A rendelést rögzítettük, de az online bankkártyás fizetés nem fejeződött be.
        </p>
        {error && (
          <div role="alert" className="mt-5 rounded-sm bg-site-danger-soft p-4 text-site-danger">
            {error}
          </div>
        )}
        <div className="mt-8 flex flex-col justify-center gap-3 sm:flex-row">
          <button
            type="button"
            onClick={() => void retrySumUpPayment(cancelledSumUpOrderId)}
            disabled={submitting}
            className="rounded-sm bg-site-green px-5 py-3 font-semibold text-white disabled:opacity-50"
          >
            {submitting ? "Átirányítás..." : "Fizetés újrapróbálása"}
          </button>
          <Link
            to={ROUTES.products}
            className="rounded-sm border border-site-border bg-white px-5 py-3 font-semibold text-site-heading"
          >
            Vissza a termékekhez
          </Link>
        </div>
      </main>
    );
  }

  if (!cart?.items.length) {
    return (
      <main className="mx-auto max-w-4xl px-4 py-12 text-center">
        <h1 className="text-3xl font-bold">A kosár üres</h1>
        <Link
          to={ROUTES.products}
          className="mt-6 inline-flex rounded-sm bg-site-green px-5 py-3 font-semibold text-white"
        >
          Vissza a termékekhez
        </Link>
      </main>
    );
  }

  return (
    <main className="mx-auto w-full max-w-7xl px-4 py-8">
      <h1 className="text-3xl font-bold text-site-heading">Pénztár</h1>
      {cancelledSumUpOrderId && (
        <div role="status" className="mt-5 rounded-sm border border-site-green/30 bg-site-surface-soft p-4 text-site-green-dark">
          Az online fizetés megszakadt. A rendelésedet rögzítettük; innen újraindíthatod a fizetést.
          <button
            type="button"
            onClick={() => void retrySumUpPayment(cancelledSumUpOrderId)}
            disabled={submitting}
            className="mt-3 block rounded-sm bg-site-green px-4 py-2 text-sm font-semibold text-white disabled:opacity-50"
          >
            {submitting ? "Átirányítás..." : "Fizetés újrapróbálása"}
          </button>
        </div>
      )}
      {error && (
        <div role="alert" className="mt-5 rounded-sm bg-site-danger-soft p-4 text-site-danger">
          {error}
        </div>
      )}

      <form onSubmit={submit} className="mt-7 grid items-start gap-7 lg:grid-cols-[minmax(0,1fr)_22rem]">
        <div className={`space-y-6 ${step === "payment" ? "hidden" : ""}`}>
          {!authenticated && (
            <fieldset className="rounded-sm border border-site-border bg-white p-6 shadow-md">
              <legend className="px-2 text-xl font-bold">Vásárlói típus</legend>
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
                    Papíralapú nyugtát kapsz a rendelés mellé.
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
                    Céges rendeléshez számlázási adatok szükségesek.
                  </span>
                </label>
              </div>
            </fieldset>
          )}

          <fieldset className="rounded-sm border border-site-border bg-white p-6 shadow-md">
            <legend className="px-2 text-xl font-bold">Átvételi mód</legend>
            <div className="grid gap-3 sm:grid-cols-2">
              <label className="rounded-sm border p-4">
                <input
                  type="radio"
                  checked={method === 0}
                  onChange={() => setMethod(0)}
                  className="mr-3"
                />
                Személyes átvétel
              </label>
              <label className={`rounded-sm border p-4 ${!deliveryAllowed ? "opacity-50" : ""}`}>
                <input
                  type="radio"
                  checked={method === 1}
                  onChange={() => setMethod(1)}
                  disabled={!deliveryAllowed}
                  className="mr-3"
                />
                Házhoz szállítás
              </label>
              <label className={`rounded-sm border p-4 ${!deliveryAllowed ? "opacity-50" : ""}`}>
                <input
                  type="radio"
                  checked={method === 2}
                  onChange={() => setMethod(2)}
                  disabled={!deliveryAllowed}
                  className="mr-3"
                />
                Csomagautomatába szállítás
              </label>
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
              {BILLING_FIELDS.map(([key, label, autoComplete]) => (
                <label key={key} className="text-sm font-semibold">
                  {label}
                  {REQUIRED_BILLING_FIELDS.includes(key) && <span className="text-site-danger"> *</span>}
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

          {method !== 0 && (
            <fieldset className="rounded-sm border border-site-border bg-white p-6 shadow-md">
              <legend className="px-2 text-xl font-bold">
                {method === 2 ? "Csomagautomata címe" : "Szállítási cím"}
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

              <div className="grid gap-4 sm:grid-cols-2">
                {DELIVERY_FIELDS.map(([key, label, autoComplete]) => (
                  <label key={key} className="text-sm font-semibold">
                    {label}
                    {REQUIRED_DELIVERY_FIELDS.includes(key) && <span className="text-site-danger"> *</span>}
                    <input
                      value={draft[key]}
                      disabled={!deliveryManual}
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
            </fieldset>
          )}
        </div>

        {step === "payment" && (
          <section className="space-y-6">
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

        <aside className="rounded-sm bg-site-surface-soft p-5 shadow-md lg:sticky lg:top-28">
          <h2 className="text-xl font-bold">Rendelés összesítése</h2>
          <ul className="mt-4 divide-y divide-site-border border-b border-site-border pb-4">
            {cart.items.map((item) => (
              <SummaryCartItem key={item.id} item={item} />
            ))}
          </ul>

          {deliveryFee > 0 && (
            <div className="mt-3 flex justify-between text-sm">
              <span>{method === 1 ? "Házhoz szállítás" : "Csomagautomata"}</span>
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
      </form>

      {showReview && (
        <div
          className="fixed inset-0 z-40 flex items-start justify-center overflow-y-auto bg-black/50 p-3 pt-24 sm:pt-28"
          role="dialog"
          aria-modal="true"
          aria-labelledby="review-title"
        >
          <section className="max-h-[calc(100vh-7rem)] w-full max-w-5xl overflow-y-auto rounded-sm bg-white p-4 shadow-xl sm:max-h-[calc(100vh-8rem)] sm:p-5">
            <div className="flex items-start justify-between gap-4">
              <h2 id="review-title" className="text-2xl font-bold text-site-heading">
                Megrendelés áttekintése
              </h2>
              <button
                type="button"
                onClick={() => setShowReview(false)}
                aria-label="Bezárás"
                className="text-2xl"
              >
                ×
              </button>
            </div>

            <div className="mt-4 space-y-3 text-sm">
              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                <div className="rounded-sm bg-site-surface-soft p-3">
                  <strong className="block">Kapcsolattartó</strong>
                  <p>{draft.customerName}</p>
                  <p>{draft.customerEmail}</p>
                  <p>{draft.customerPhone}</p>
                </div>

                <div className="space-y-3">
                  <div className="rounded-sm bg-site-surface-soft p-3">
                    <strong className="block">Rendelési mód</strong>
                    <p>
                      {method === 0
                        ? "Személyes átvétel"
                        : method === 1
                          ? "Házhoz szállítás"
                          : "Csomagautomatába szállítás"}
                    </p>
                  </div>
                  <div className="rounded-sm bg-site-surface-soft p-3">
                    <strong className="block">Fizetés</strong>
                    <p>{paymentLabel(paymentMethod)}</p>
                  </div>
                </div>

                <div className="space-y-3">
                  <div className="rounded-sm bg-site-surface-soft p-3">
                    <strong className="block">Számlázási cím</strong>
                    {effectiveCustomerType === 1 && draft.billingCompanyName && (
                      <p>
                        {draft.billingCompanyName} · {draft.billingTaxNumber}
                      </p>
                    )}
                    <p>
                      {effectiveCustomerType === 1
                        ? `${draft.postalCode} ${draft.city}, ${draft.street} ${draft.houseNumber}`
                        : "Papíralapú nyugta a rendelés mellé."}
                    </p>
                    {effectiveCustomerType === 1 && (
                      <p>
                        {draft.printedInvoiceRequested
                          ? "Elektronikus és nyomtatott számla"
                          : "Elektronikus számla"}
                      </p>
                    )}
                  </div>
                  <div
                    className={`rounded-sm p-3 ${method !== 0 ? "bg-site-surface-soft" : "invisible"}`}
                    aria-hidden={method === 0}
                  >
                    <strong className="block">Szállítási cím</strong>
                    {method !== 0 && (
                      <p>
                        {`${draft.deliveryPostalCode} ${draft.deliveryCity}, ${draft.deliveryStreet} ${draft.deliveryHouseNumber}`}
                      </p>
                    )}
                  </div>
                </div>
              </div>

              <div className="border-y border-site-border py-3">
                <ul className="divide-y divide-site-border">
                  {cart.items.map((item) => (
                    <SummaryCartItem key={item.id} item={item} compact />
                  ))}
                </ul>
              </div>

              <p className="flex justify-between text-lg font-bold">
                <span>Összesen</span>
                <span>{PRICE.format(grandTotal)}</span>
              </p>

              <div className="space-y-3">
                <div>
                  <label className="flex items-start gap-3">
                    <input
                      type="checkbox"
                      checked={termsAccepted}
                      onChange={(event) => {
                        setTermsAccepted(event.target.checked);
                        setFieldErrors((current) => ({ ...current, terms: undefined }));
                      }}
                      className="mt-1"
                    />
                    <span>
                      Elolvastam és elfogadom az{" "}
                      <Link to={ROUTES.terms} className="font-semibold text-site-green-dark underline">
                        Általános Szerződési Feltételeket
                      </Link>
                      . <span className="text-site-danger">*</span>
                    </span>
                  </label>
                  {fieldErrors.terms && (
                    <p className="mt-1 text-xs text-site-danger">{fieldErrors.terms}</p>
                  )}
                </div>

                <div>
                  <label className="flex items-start gap-3">
                    <input
                      type="checkbox"
                      checked={privacyAccepted}
                      onChange={(event) => {
                        setPrivacyAccepted(event.target.checked);
                        setFieldErrors((current) => ({ ...current, privacy: undefined }));
                      }}
                      className="mt-1"
                    />
                    <span>
                      Elolvastam és elfogadom az{" "}
                      <Link to={ROUTES.privacyPolicy} className="font-semibold text-site-green-dark underline">
                        Adatkezelési Tájékoztatót
                      </Link>
                      . <span className="text-site-danger">*</span>
                    </span>
                  </label>
                  {fieldErrors.privacy && (
                    <p className="mt-1 text-xs text-site-danger">{fieldErrors.privacy}</p>
                  )}
                </div>
              </div>
            </div>

            <div className="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
              <button
                type="button"
                onClick={() => setShowReview(false)}
                className="rounded-sm border border-site-border px-5 py-3"
              >
                Vissza a szerkesztéshez
              </button>
              <button
                type="button"
                onClick={() => void placeOrder()}
                disabled={submitting}
                className="rounded-sm bg-site-green px-5 py-3 font-semibold text-white disabled:opacity-50"
              >
                {submitting
                  ? paymentMethod === "online-card"
                    ? "Átirányítás..."
                    : "Rendelés elküldése..."
                  : paymentMethod === "online-card"
                    ? "Tovább a fizetéshez"
                    : "Megrendelés elküldése"}
              </button>
            </div>
          </section>
        </div>
      )}
    </main>
  );
}
