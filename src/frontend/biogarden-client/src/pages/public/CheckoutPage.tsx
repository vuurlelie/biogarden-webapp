import { useEffect, useMemo, useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { createOrder } from "../../features/account/api/ordersApi";
import { getAddresses, getProfile } from "../../features/account/api/profileApi";
import type { UserAddresses } from "../../features/account/types/address.types";
import type { UserProfile } from "../../features/account/types/profile.types";
import { getAuthSession } from "../../features/auth/utils/authStorage";
import { validateCart } from "../../features/cart/api/cartApi";
import { useCart } from "../../features/cart/context/useCart";
import { ROUTES } from "../../shared/constants/routes";
import { getUnitLabel } from "../../features/products/utils/productLabels";

const DRAFT_KEY = "biogarden_checkout_draft";
const PRICE = new Intl.NumberFormat("hu-HU", { style: "currency", currency: "HUF", maximumFractionDigits: 0 });
type FulfillmentMethod = 0 | 1 | 2;
type PaymentMethod = "online-card" | "pay-at-pickup" | "cash-on-delivery";
type Draft = {
  customerName: string; customerEmail: string; customerPhone: string;
  postalCode: string; city: string; street: string; houseNumber: string;
  building: string; floor: string; door: string; doorbell: string;
  billingCompanyName: string; billingTaxNumber: string;
  deliveryPostalCode: string; deliveryCity: string; deliveryStreet: string; deliveryHouseNumber: string;
  deliveryBuilding: string; deliveryFloor: string; deliveryDoor: string; deliveryDoorbell: string;
};
type FieldErrors = Partial<Record<keyof Draft | "accountContact" | "billingAddress" | "deliveryAddress" | "terms" | "privacy", string>>;
const EMPTY_DRAFT: Draft = { customerName: "", customerEmail: "", customerPhone: "", postalCode: "", city: "", street: "", houseNumber: "", building: "", floor: "", door: "", doorbell: "", billingCompanyName: "", billingTaxNumber: "", deliveryPostalCode: "", deliveryCity: "", deliveryStreet: "", deliveryHouseNumber: "", deliveryBuilding: "", deliveryFloor: "", deliveryDoor: "", deliveryDoorbell: "" };

function loadDraft(): Draft {
  try { return { ...EMPTY_DRAFT, ...JSON.parse(sessionStorage.getItem(DRAFT_KEY) ?? "{}") }; }
  catch { return EMPTY_DRAFT; }
}

export default function CheckoutPage() {
  const navigate = useNavigate();
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
  const [step, setStep] = useState<"details" | "payment">("details");
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod | null>(null);
  const hasDraft = Object.values(draft).some(Boolean);
  const deliveryAllowed = useMemo(() => Boolean(cart?.items.length && cart.items.every((item) => item.deliveryAvailable)), [cart]);

  useEffect(() => { sessionStorage.setItem(DRAFT_KEY, JSON.stringify(draft)); }, [draft]);
  useEffect(() => {
    if (!authenticated) { setLoading(false); return; }
    let ignore = false;
    void Promise.all([getAddresses(), getProfile()]).then(([value, loadedProfile]) => {
      if (ignore) return;
      setAddresses(value);
      setProfile(loadedProfile);
      if (!hasDraft) {
        setContactManual(false);
        setBillingManual(!value.billingAddress);
        setDeliveryManual(!value.shippingAddress);
        setDraft((current) => ({
          ...current,
          customerName: [loadedProfile.lastName, loadedProfile.firstName].filter(Boolean).join(" "),
          customerEmail: loadedProfile.email ?? "",
          customerPhone: loadedProfile.phoneNumber ?? "",
          billingCompanyName: loadedProfile.companyName ?? "",
          billingTaxNumber: loadedProfile.taxNumber ?? "",
          ...(value.billingAddress ? { postalCode: value.billingAddress.postalCode, city: value.billingAddress.city, street: value.billingAddress.street, houseNumber: value.billingAddress.houseNumber, building: value.billingAddress.building ?? "", floor: value.billingAddress.floor ?? "", door: value.billingAddress.door ?? "", doorbell: value.billingAddress.doorbell ?? "" } : {}),
          ...(value.shippingAddress ? { deliveryPostalCode: value.shippingAddress.postalCode, deliveryCity: value.shippingAddress.city, deliveryStreet: value.shippingAddress.street, deliveryHouseNumber: value.shippingAddress.houseNumber, deliveryBuilding: value.shippingAddress.building ?? "", deliveryFloor: value.shippingAddress.floor ?? "", deliveryDoor: value.shippingAddress.door ?? "", deliveryDoorbell: value.shippingAddress.doorbell ?? "" } : {}),
        }));
      }
    }).catch((e) => { if (!ignore) setError(e instanceof Error ? e.message : "A fiókadatok betöltése nem sikerült."); })
      .finally(() => { if (!ignore) setLoading(false); });
    return () => { ignore = true; };
  }, [authenticated, hasDraft]);
  useEffect(() => { if (!deliveryAllowed && method !== 0) setMethod(0); }, [deliveryAllowed, method]);
  useEffect(() => { setPaymentMethod(null); }, [method]);

  function update(field: keyof Draft, value: string) {
    setDraft((current) => ({ ...current, [field]: value }));
    setFieldErrors((current) => ({ ...current, [field]: undefined }));
  }
  function fieldClass(field: keyof Draft): string {
    const base = "mt-2 block w-full rounded-sm border px-3 py-2.5 disabled:cursor-default";
    if (fieldErrors[field]) return `${base} border-site-danger bg-white`;
    const value = draft[field].trim();
    const valid = field === "customerName" ? value.length >= 2
      : field === "customerEmail" ? /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value)
      : field === "customerPhone" ? value.replace(/\D/g, "").length >= 9
      : field === "postalCode" || field === "deliveryPostalCode" ? /^\d{4}$/.test(value)
      : field === "city" || field === "street" || field === "deliveryCity" || field === "deliveryStreet" ? value.length >= 2
      : field === "houseNumber" || field === "deliveryHouseNumber" ? value.length > 0
      : field === "billingCompanyName" ? value.length > 0 && /^\d{8}(-\d-\d{2})?$/.test(draft.billingTaxNumber.trim())
      : field === "billingTaxNumber" ? /^\d{8}(-\d-\d{2})?$/.test(value) && draft.billingCompanyName.trim().length > 0
      : value.length > 0;
    if (valid) return `${base} border-site-green bg-site-surface-soft`;
    return `${base} border-site-border bg-white`;
  }
  function handleAccountContactChange(checked: boolean) {
    setContactManual(!checked);
    setFieldErrors((current) => ({ ...current, customerName: undefined, customerEmail: undefined, customerPhone: undefined, accountContact: undefined }));
    setDraft((current) => checked && profile
      ? { ...current, customerName: [profile.lastName, profile.firstName].filter(Boolean).join(" "), customerEmail: profile.email ?? "", customerPhone: profile.phoneNumber ?? "" }
      : { ...current, customerName: "", customerEmail: "", customerPhone: "" });
  }
  function handleAccountBillingChange(checked: boolean) {
    setBillingManual(!checked);
    setFieldErrors((current) => ({ ...current, billingCompanyName: undefined, billingTaxNumber: undefined, postalCode: undefined, city: undefined, street: undefined, houseNumber: undefined, billingAddress: undefined }));
    const address = addresses?.billingAddress;
    setDraft((current) => checked && address
      ? { ...current, billingCompanyName: profile?.companyName ?? "", billingTaxNumber: profile?.taxNumber ?? "", postalCode: address.postalCode, city: address.city, street: address.street, houseNumber: address.houseNumber, building: address.building ?? "", floor: address.floor ?? "", door: address.door ?? "", doorbell: address.doorbell ?? "" }
      : { ...current, billingCompanyName: "", billingTaxNumber: "", postalCode: "", city: "", street: "", houseNumber: "", building: "", floor: "", door: "", doorbell: "" });
  }
  function handleAccountDeliveryChange(checked: boolean) {
    setDeliveryManual(!checked);
    setFieldErrors((current) => ({ ...current, deliveryPostalCode: undefined, deliveryCity: undefined, deliveryStreet: undefined, deliveryHouseNumber: undefined, deliveryAddress: undefined }));
    const address = addresses?.shippingAddress;
    setDraft((current) => checked && address
      ? { ...current, deliveryPostalCode: address.postalCode, deliveryCity: address.city, deliveryStreet: address.street, deliveryHouseNumber: address.houseNumber, deliveryBuilding: address.building ?? "", deliveryFloor: address.floor ?? "", deliveryDoor: address.door ?? "", deliveryDoorbell: address.doorbell ?? "" }
      : { ...current, deliveryPostalCode: "", deliveryCity: "", deliveryStreet: "", deliveryHouseNumber: "", deliveryBuilding: "", deliveryFloor: "", deliveryDoor: "", deliveryDoorbell: "" });
  }
  function validateFields(): FieldErrors {
    const errors: FieldErrors = {};
    if (draft.customerName.trim().length < 2) errors.customerName = "A név megadása kötelező, legalább 2 karakterrel.";
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(draft.customerEmail.trim())) errors.customerEmail = "Adj meg egy érvényes email címet.";
    if (draft.customerPhone.replace(/\D/g, "").length < 9) errors.customerPhone = "Adj meg egy érvényes telefonszámot.";
    if (!/^\d{4}$/.test(draft.postalCode.trim())) errors.postalCode = "Az irányítószám 4 számjegyből álljon.";
    if (draft.city.trim().length < 2) errors.city = "A település megadása kötelező.";
    if (draft.street.trim().length < 2) errors.street = "A közterület megadása kötelező.";
    if (!draft.houseNumber.trim()) errors.houseNumber = "A házszám megadása kötelező.";
    if (Boolean(draft.billingCompanyName.trim()) !== Boolean(draft.billingTaxNumber.trim())) {
      errors.billingCompanyName = "Céges számlázáshoz a cégnév és az adószám együtt szükséges.";
      errors.billingTaxNumber = "Céges számlázáshoz a cégnév és az adószám együtt szükséges.";
    } else if (draft.billingTaxNumber.trim() && !/^\d{8}(-\d-\d{2})?$/.test(draft.billingTaxNumber.trim())) errors.billingTaxNumber = "Hibás adószám. Példa: 12345678-1-12.";
    if (method !== 0) {
      if (!/^\d{4}$/.test(draft.deliveryPostalCode.trim())) errors.deliveryPostalCode = "Az irányítószám 4 számjegyből álljon.";
      if (draft.deliveryCity.trim().length < 2) errors.deliveryCity = "A település megadása kötelező.";
      if (draft.deliveryStreet.trim().length < 2) errors.deliveryStreet = "A közterület megadása kötelező.";
      if (!draft.deliveryHouseNumber.trim()) errors.deliveryHouseNumber = "A házszám megadása kötelező.";
    }
    return errors;
  }
  async function submit(event: FormEvent) {
    event.preventDefault(); setError("");
    if (!cart?.items.length) return setError("A kosár üres.");
    const errors = validateFields();
    setFieldErrors(errors);
    if (Object.keys(errors).length) { setError("Ellenőrizd a pirossal jelölt mezőket."); return; }
    setSubmitting(true);
    try {
      const validation = await validateCart();
      if (!validation.isValid) { setError(validation.issues.map((i) => i.message).join(" ")); await refresh(); return; }
      if (step === "details") setStep("payment"); else setShowReview(true);
    } catch (e) { setError(e instanceof Error ? e.message : "A kosár ellenőrzése nem sikerült."); }
    finally { setSubmitting(false); }
  }

  async function placeOrder() {
    const consentErrors: FieldErrors = {};
    if (!termsAccepted) consentErrors.terms = "Az Általános Szerződési Feltételek elfogadása kötelező.";
    if (!privacyAccepted) consentErrors.privacy = "Az Adatkezelési Tájékoztató elfogadása kötelező.";
    setFieldErrors((current) => ({ ...current, ...consentErrors }));
    if (Object.keys(consentErrors).length) return;
    setSubmitting(true); setError("");
    try {
      const order = await createOrder({
        fulfillmentMethod: method,
        customerName: draft.customerName, customerEmail: draft.customerEmail, customerPhone: draft.customerPhone,
        addressId: null,
        postalCode: draft.postalCode, city: draft.city, street: draft.street, houseNumber: draft.houseNumber, building: draft.building, floor: draft.floor, door: draft.door, doorbell: draft.doorbell,
        billingCompanyName: draft.billingCompanyName, billingTaxNumber: draft.billingTaxNumber,
        deliveryAddressId: null,
        ...(method !== 0 ? { deliveryPostalCode: draft.deliveryPostalCode, deliveryCity: draft.deliveryCity, deliveryStreet: draft.deliveryStreet, deliveryHouseNumber: draft.deliveryHouseNumber, deliveryBuilding: draft.deliveryBuilding, deliveryFloor: draft.deliveryFloor, deliveryDoor: draft.deliveryDoor, deliveryDoorbell: draft.deliveryDoorbell } : {}),
      });
      sessionStorage.removeItem(DRAFT_KEY);
      await refresh();
      navigate(ROUTES.orderSuccess, { replace: true, state: { order } });
    } catch (e) { setShowReview(false); setError(e instanceof Error ? e.message : "A rendelést nem sikerült elküldeni."); }
    finally { setSubmitting(false); }
  }

  if (loading || cartLoading) return <div className="mx-auto max-w-7xl px-4 py-12 text-center">A pénztár betöltése...</div>;
  if (!cart?.items.length) return <main className="mx-auto max-w-4xl px-4 py-12 text-center"><h1 className="text-3xl font-bold">A kosár üres</h1><Link to={ROUTES.products} className="mt-6 inline-flex rounded-sm bg-site-green px-5 py-3 font-semibold text-white">Vissza a termékekhez</Link></main>;
  const contactFields: Array<[keyof Draft, string, string]> = [["customerName","Név","name"],["customerEmail","Email cím","email"],["customerPhone","Telefonszám","tel"]];
  const billingFields: Array<[keyof Draft, string, string]> = [["billingCompanyName","Cégnév (opcionális)","organization"],["billingTaxNumber","Adószám (céges számlához)","off"],["postalCode","Irányítószám","postal-code"],["city","Település","address-level2"],["street","Közterület neve és jellege","address-line1"],["houseNumber","Házszám","address-line2"],["building","Épület","off"],["floor","Emelet","off"],["door","Ajtó","off"],["doorbell","Kapucsengő","off"]];
  const deliveryFields: Array<[keyof Draft, string, string]> = [["deliveryPostalCode","Irányítószám","shipping postal-code"],["deliveryCity","Település","shipping address-level2"],["deliveryStreet","Közterület neve és jellege","shipping address-line1"],["deliveryHouseNumber","Házszám","shipping address-line2"],["deliveryBuilding","Épület","off"],["deliveryFloor","Emelet","off"],["deliveryDoor","Ajtó","off"],["deliveryDoorbell","Kapucsengő","off"]];
  const deliveryFee = method === 1 ? 1500 : method === 2 ? 1000 : 0;
  const paymentFee = paymentMethod === "cash-on-delivery" ? 500 : 0;
  const grandTotal = cart.totalPrice + deliveryFee + paymentFee;

  return <main className="mx-auto w-full max-w-7xl px-4 py-8">
    <h1 className="text-3xl font-bold text-site-heading">Pénztár</h1>
    {error && <div role="alert" className="mt-5 rounded-sm bg-site-danger-soft p-4 text-site-danger">{error}</div>}
    <form onSubmit={submit} className="mt-7 grid items-start gap-7 lg:grid-cols-[minmax(0,1fr)_22rem]">
      <div className={`space-y-6 ${step === "payment" ? "hidden" : ""}`}>
        <fieldset className="rounded-sm border border-site-border bg-white p-6 shadow-md"><legend className="px-2 text-xl font-bold">Átvételi mód</legend><div className="grid gap-3 sm:grid-cols-2">
          <label className="rounded-sm border p-4"><input type="radio" checked={method===0} onChange={()=>setMethod(0)} className="mr-3"/>Személyes átvétel</label>
          <label className={`rounded-sm border p-4 ${!deliveryAllowed ? "opacity-50" : ""}`}><input type="radio" checked={method===1} onChange={()=>setMethod(1)} disabled={!deliveryAllowed} className="mr-3"/>Házhoz szállítás</label>
          <label className={`rounded-sm border p-4 ${!deliveryAllowed ? "opacity-50" : ""}`}><input type="radio" checked={method===2} onChange={()=>setMethod(2)} disabled={!deliveryAllowed} className="mr-3"/>Csomagautomatába szállítás</label>
        </div></fieldset>
        {!authenticated && <p className="rounded-sm bg-site-surface-soft p-4 text-sm">Felhasználói fiókból történő rendeléshez jelentkezz be itt: <Link to={ROUTES.login} state={{ from: ROUTES.checkout }} className="font-semibold underline">Bejelentkezés</Link></p>}
        <fieldset className="rounded-sm border border-site-border bg-white p-6 shadow-md"><legend className="px-2 text-xl font-bold">Kapcsolattartási adatok</legend>
          {authenticated && <label className="mb-4 flex items-center gap-3 text-sm font-semibold"><input type="checkbox" checked={!contactManual} onChange={(e)=>handleAccountContactChange(e.target.checked)}/>Fiókban mentett személyes adatok betöltése</label>}
          <div className="grid gap-4 sm:grid-cols-2">{contactFields.map(([key,label,auto])=><label key={key} className="text-sm font-semibold">{label} <span className="text-site-danger">*</span><input value={draft[key]} onChange={(e)=>update(key,e.target.value)} type={key==="customerEmail"?"email":key==="customerPhone"?"tel":"text"} autoComplete={auto} aria-invalid={Boolean(fieldErrors[key])} className={fieldClass(key)} />{fieldErrors[key]&&<span className="mt-1 block text-xs text-site-danger">{fieldErrors[key]}</span>}</label>)}</div>
        </fieldset>
        <fieldset className="rounded-sm border border-site-border bg-white p-6 shadow-md"><legend className="px-2 text-xl font-bold">Számlázási cím</legend>{authenticated&&<label className="mb-4 flex items-center gap-3 text-sm font-semibold"><input type="checkbox" checked={!billingManual} disabled={!addresses?.billingAddress} onChange={(e)=>handleAccountBillingChange(e.target.checked)}/>Fiókban mentett számlázási cím használata</label>}{authenticated&&!addresses?.billingAddress&&<p className="mb-4 text-sm text-site-danger">A fiókban nincs mentett számlázási cím; töltsd ki az alábbi mezőket.</p>}<div className="grid gap-4 sm:grid-cols-2">{billingFields.map(([key,label,auto])=><label key={key} className="text-sm font-semibold">{label}{["postalCode","city","street","houseNumber"].includes(key)&&<span className="text-site-danger"> *</span>}<input value={draft[key]} disabled={!billingManual} onChange={(e)=>update(key,e.target.value)} autoComplete={auto} aria-invalid={Boolean(fieldErrors[key])} className={fieldClass(key)}/>{fieldErrors[key]&&<span className="mt-1 block text-xs text-site-danger">{fieldErrors[key]}</span>}</label>)}</div></fieldset>
        {method!==0 && <fieldset className="rounded-sm border border-site-border bg-white p-6 shadow-md"><legend className="px-2 text-xl font-bold">{method===2?"Csomagautomata címe":"Szállítási cím"}</legend>{authenticated&&<label className="mb-4 flex items-center gap-3 text-sm font-semibold"><input type="checkbox" checked={!deliveryManual} disabled={!addresses?.shippingAddress} onChange={(e)=>handleAccountDeliveryChange(e.target.checked)}/>Fiókban mentett szállítási cím használata</label>}{authenticated&&!addresses?.shippingAddress&&<p className="mb-4 text-sm text-site-danger">A fiókban nincs mentett szállítási cím; töltsd ki az alábbi mezőket.</p>}<div className="grid gap-4 sm:grid-cols-2">{deliveryFields.map(([key,label,auto])=><label key={key} className="text-sm font-semibold">{label}{["deliveryPostalCode","deliveryCity","deliveryStreet","deliveryHouseNumber"].includes(key)&&<span className="text-site-danger"> *</span>}<input value={draft[key]} disabled={!deliveryManual} onChange={(e)=>update(key,e.target.value)} autoComplete={auto} aria-invalid={Boolean(fieldErrors[key])} className={fieldClass(key)}/>{fieldErrors[key]&&<span className="mt-1 block text-xs text-site-danger">{fieldErrors[key]}</span>}</label>)}</div></fieldset>}
      </div>
      {step === "payment" && <section className="space-y-6"><button type="button" onClick={()=>setStep("details")} className="text-sm font-semibold text-site-green-dark underline">← Vissza az adatokhoz</button><fieldset className="rounded-sm border border-site-border bg-white p-6 shadow-md"><legend className="px-2 text-xl font-bold">Fizetési mód</legend><div className="grid gap-3"><label className="rounded-sm border p-4"><input type="radio" checked={paymentMethod==="online-card"} onChange={()=>setPaymentMethod("online-card")} className="mr-3"/><strong>Online bankkártyás fizetés</strong><span className="mt-1 block text-sm text-site-muted">Biztonságos online kártyás fizetés – hamarosan elérhető.</span></label>{method===0?<label className="rounded-sm border p-4"><input type="radio" checked={paymentMethod==="pay-at-pickup"} onChange={()=>setPaymentMethod("pay-at-pickup")} className="mr-3"/><strong>Fizetés átvételkor</strong><span className="mt-1 block text-sm text-site-muted">A kertészetben készpénzzel vagy bankkártyával.</span></label>:<label className="rounded-sm border p-4"><input type="radio" checked={paymentMethod==="cash-on-delivery"} onChange={()=>setPaymentMethod("cash-on-delivery")} className="mr-3"/><strong>Utánvét</strong><span className="mt-1 block text-sm text-site-muted">Teszt utánvételi díj: {PRICE.format(500)}.</span></label>}</div>{paymentMethod==="online-card"&&<div className="mt-5 rounded-sm border border-dashed border-site-green p-5 text-sm"><strong>Bankkártyás fizetési felület helye</strong><p className="mt-1 text-site-muted">Itt fog megjelenni a fizetési szolgáltató biztonságos kártyaeleme. Kártyaadatot a saját szerverünk nem kezelhet.</p></div>}</fieldset></section>}
      <aside className="rounded-sm bg-site-surface-soft p-5 shadow-md lg:sticky lg:top-28"><h2 className="text-xl font-bold">Rendelés összesítése</h2><ul className="mt-4 space-y-3 border-b pb-4">{cart.items.map((item)=><li key={item.id} className="flex justify-between text-sm"><span>{item.productName} × {item.quantity}</span><strong>{PRICE.format(item.lineTotal)}</strong></li>)}</ul>{deliveryFee>0&&<div className="mt-3 flex justify-between text-sm"><span>{method===1?"Házhoz szállítás":"Csomagautomata"}</span><strong>{PRICE.format(deliveryFee)}</strong></div>}{paymentFee>0&&<div className="mt-2 flex justify-between text-sm"><span>Utánvét</span><strong>{PRICE.format(paymentFee)}</strong></div>}<div className="mt-4 flex justify-between border-t border-site-border pt-4 text-lg font-bold"><span>Összesen</span><span>{PRICE.format(grandTotal)}</span></div><button type="submit" disabled={submitting||step==="payment"&&(paymentMethod===null||paymentMethod==="online-card")} className="mt-5 w-full rounded-sm bg-site-green px-5 py-3 font-semibold text-white disabled:opacity-50">{submitting?"Ellenőrzés...":step==="details"?"Tovább a pénztárhoz":"Rendelés áttekintése"}</button>{step==="payment"&&paymentMethod===null&&<p className="mt-2 text-xs text-site-muted">Válassz fizetési módot a folytatáshoz.</p>}{step==="payment"&&paymentMethod==="online-card"&&<p className="mt-2 text-xs text-site-muted">Az online fizetés jelenleg csak bemutató; válassz másik fizetési módot a tesztrendeléshez.</p>}</aside>
    </form>
    {showReview && <div className="fixed inset-0 z-[80] flex items-center justify-center bg-black/50 p-3" role="dialog" aria-modal="true" aria-labelledby="review-title"><section className="max-h-[96vh] w-full max-w-5xl overflow-y-auto rounded-sm bg-white p-4 shadow-xl sm:p-5"><div className="flex items-start justify-between gap-4"><h2 id="review-title" className="text-2xl font-bold text-site-heading">Megrendelés áttekintése</h2><button type="button" onClick={()=>setShowReview(false)} aria-label="Bezárás" className="text-2xl">×</button></div>
      <div className="mt-4 space-y-3 text-sm"><div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3"><div className="rounded-sm bg-site-surface-soft p-3"><strong className="block">Kapcsolattartó</strong><p>{draft.customerName}</p><p>{draft.customerEmail}</p><p>{draft.customerPhone}</p></div><div className="space-y-3"><div className="rounded-sm bg-site-surface-soft p-3"><strong className="block">Rendelési mód</strong><p>{method===0?"Személyes átvétel":method===1?"Házhoz szállítás":"Csomagautomatába szállítás"}</p></div><div className="rounded-sm bg-site-surface-soft p-3"><strong className="block">Fizetés</strong><p>{paymentMethod==="pay-at-pickup"?"Fizetés átvételkor":paymentMethod==="cash-on-delivery"?"Utánvét":"Online bankkártya"}</p></div></div><div className="space-y-3"><div className="rounded-sm bg-site-surface-soft p-3"><strong className="block">Számlázási cím</strong>{draft.billingCompanyName&&<p>{draft.billingCompanyName} · {draft.billingTaxNumber}</p>}<p>{`${draft.postalCode} ${draft.city}, ${draft.street} ${draft.houseNumber}`}</p></div><div className={`rounded-sm p-3 ${method!==0?"bg-site-surface-soft":"invisible"}`} aria-hidden={method===0}><strong className="block">Szállítási cím</strong>{method!==0&&<p>{`${draft.deliveryPostalCode} ${draft.deliveryCity}, ${draft.deliveryStreet} ${draft.deliveryHouseNumber}`}</p>}</div></div></div>
        <div className="border-y border-site-border py-3"><div className="mb-2 hidden grid-cols-[minmax(10rem,1fr)_8rem_6.5rem] gap-4 text-xs font-bold uppercase text-site-muted sm:grid"><span>Termék</span><span>Egységár</span><span className="text-right">Összeg</span></div><ul className="space-y-1">{cart.items.map((item)=><li key={item.id} className="grid gap-1 sm:grid-cols-[minmax(10rem,1fr)_8rem_6.5rem] sm:gap-4"><span>{item.productName} × {item.quantity}</span><span className="text-site-muted">{PRICE.format(item.unitPrice)} / {getUnitLabel(item.unit)}</span><strong className="sm:text-right">{PRICE.format(item.lineTotal)}</strong></li>)}</ul></div><p className="flex justify-between text-lg font-bold"><span>Összesen</span><span>{PRICE.format(grandTotal)}</span></p>
        <div className="space-y-3"><div><label className="flex items-start gap-3"><input type="checkbox" checked={termsAccepted} onChange={(e)=>{setTermsAccepted(e.target.checked);setFieldErrors((v)=>({...v,terms:undefined}));}} className="mt-1"/><span>Elolvastam és elfogadom az Általános Szerződési Feltételeket. <span className="text-site-danger">*</span></span></label>{fieldErrors.terms&&<p className="mt-1 text-xs text-site-danger">{fieldErrors.terms}</p>}</div><div><label className="flex items-start gap-3"><input type="checkbox" checked={privacyAccepted} onChange={(e)=>{setPrivacyAccepted(e.target.checked);setFieldErrors((v)=>({...v,privacy:undefined}));}} className="mt-1"/><span>Elolvastam és elfogadom az Adatkezelési Tájékoztatót. <span className="text-site-danger">*</span></span></label>{fieldErrors.privacy&&<p className="mt-1 text-xs text-site-danger">{fieldErrors.privacy}</p>}</div></div>
      </div><div className="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end"><button type="button" onClick={()=>setShowReview(false)} className="rounded-sm border border-site-border px-5 py-3">Vissza a szerkesztéshez</button><button type="button" onClick={()=>void placeOrder()} disabled={submitting} className="rounded-sm bg-site-green px-5 py-3 font-semibold text-white disabled:opacity-50">{submitting?"Rendelés elküldése...":"Megrendelés elküldése"}</button></div></section></div>}
  </main>;
}
