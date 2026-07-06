import { useEffect, useMemo, useState, type FormEvent } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { createOrder } from "../../features/account/api/ordersApi";
import { getAddresses, ProfileApiError } from "../../features/account/api/profileApi";
import type { AddressDetails, UserAddresses } from "../../features/account/types/address.types";
import { getAuthSession } from "../../features/auth/utils/authStorage";
import { validateCart } from "../../features/cart/api/cartApi";
import { useCart } from "../../features/cart/context/useCart";
import { ROUTES } from "../../shared/constants/routes";

const PRICE = new Intl.NumberFormat("hu-HU", {
  style: "currency",
  currency: "HUF",
  maximumFractionDigits: 0,
});

type FulfillmentMethod = 0 | 1;

function AddressOption({ address, selected, onSelect }: {
  address: AddressDetails;
  selected: boolean;
  onSelect: () => void;
}) {
  return (
    <label className={`flex cursor-pointer gap-3 rounded-sm border p-4 transition ${selected ? "border-site-green-dark bg-site-surface-soft" : "border-site-border hover:border-site-green"}`}>
      <input type="radio" name="address" checked={selected} onChange={onSelect} className="mt-1 accent-site-green" />
      <span className="text-sm leading-6 text-site-text">{address.fullAddress}</span>
    </label>
  );
}

export default function CheckoutPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { cart, isLoading: isCartLoading, refresh } = useCart();
  const [addresses, setAddresses] = useState<UserAddresses | null>(null);
  const [method, setMethod] = useState<FulfillmentMethod>(0);
  const [addressId, setAddressId] = useState<number | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState("");

  const deliveryAllowed = useMemo(
    () => Boolean(cart?.items.length && cart.items.every((item) => item.deliveryAvailable)),
    [cart],
  );

  useEffect(() => {
    if (!getAuthSession()) {
      navigate(ROUTES.login, { replace: true, state: { from: location.pathname } });
      return;
    }

    let ignore = false;
    void getAddresses()
      .then((loaded) => {
        if (ignore) return;
        setAddresses(loaded);
        setAddressId(loaded.shippingAddress?.id ?? loaded.billingAddress?.id ?? null);
      })
      .catch((loadError) => {
        if (ignore) return;
        if (loadError instanceof ProfileApiError && loadError.status === 401) {
          navigate(ROUTES.login, { replace: true });
          return;
        }
        setError(loadError instanceof Error ? loadError.message : "A címek betöltése nem sikerült.");
      })
      .finally(() => { if (!ignore) setIsLoading(false); });

    return () => { ignore = true; };
  }, [location.pathname, navigate]);

  useEffect(() => {
    if (!deliveryAllowed && method === 1) setMethod(0);
  }, [deliveryAllowed, method]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setError("");
    if (!cart?.items.length) return setError("A kosár üres.");
    if (!addressId) return setError("A rendeléshez előbb ments el egy címet.");

    setIsSubmitting(true);
    try {
      const validation = await validateCart();
      if (!validation.isValid) {
        setError(validation.issues.map((issue) => issue.message).join(" "));
        await refresh();
        return;
      }

      const order = await createOrder(addressId, method);
      await refresh();
      navigate(ROUTES.accountOrderDetails.replace(":orderId", order.id), { replace: true });
    } catch (submitError) {
      setError(submitError instanceof Error ? submitError.message : "A rendelést nem sikerült elküldeni.");
    } finally {
      setIsSubmitting(false);
    }
  }

  if (isLoading || isCartLoading) {
    return <div className="mx-auto max-w-7xl px-4 py-12 text-center text-site-text">A pénztár betöltése...</div>;
  }

  if (!cart?.items.length) {
    return (
      <main className="mx-auto max-w-4xl px-4 py-12 text-center">
        <h1 className="text-3xl font-bold text-site-heading">A kosár üres</h1>
        <Link to={ROUTES.products} className="mt-6 inline-flex rounded-sm bg-site-green px-5 py-3 font-semibold text-white hover:bg-site-green-hover">Vissza a termékekhez</Link>
      </main>
    );
  }

  const availableAddresses = [addresses?.shippingAddress, addresses?.billingAddress]
    .filter((address): address is AddressDetails => Boolean(address))
    .filter((address, index, all) => all.findIndex((candidate) => candidate.id === address.id) === index);

  return (
    <main className="mx-auto w-full max-w-7xl px-4 py-6 sm:px-6 lg:px-8">
      <nav className="mb-5 flex gap-2 text-sm text-site-text" aria-label="Morzsamenü">
        <Link to={ROUTES.home} className="font-bold text-site-green-dark hover:underline">Kezdőlap</Link><span>/</span>
        <Link to={ROUTES.cart} className="font-bold text-site-green-dark hover:underline">Kosár</Link><span>/</span>
        <span>Pénztár</span>
      </nav>
      <h1 className="text-3xl font-bold text-site-heading sm:text-4xl">Pénztár</h1>
      {error && <div role="alert" className="mt-5 rounded-sm bg-site-danger-soft p-4 text-site-danger">{error}</div>}

      <form onSubmit={submit} className="mt-7 grid items-start gap-7 lg:grid-cols-[minmax(0,1fr)_22rem]">
        <div className="space-y-6">
          <fieldset className="rounded-sm border border-site-border bg-white p-5 shadow-md sm:p-6">
            <legend className="px-2 text-xl font-bold text-site-heading">Átvételi mód</legend>
            <div className="mt-2 grid gap-3 sm:grid-cols-2">
              <label className={`cursor-pointer rounded-sm border p-4 ${method === 0 ? "border-site-green-dark bg-site-surface-soft" : "border-site-border"}`}>
                <input type="radio" name="method" checked={method === 0} onChange={() => setMethod(0)} className="mr-3 accent-site-green" />
                <strong>Személyes átvétel</strong>
                <span className="mt-2 block text-sm text-site-text">Átvétel a biokertészetben, előzetes egyeztetés után.</span>
              </label>
              <label className={`rounded-sm border p-4 ${deliveryAllowed ? "cursor-pointer" : "cursor-not-allowed opacity-60"} ${method === 1 ? "border-site-green-dark bg-site-surface-soft" : "border-site-border"}`}>
                <input type="radio" name="method" checked={method === 1} onChange={() => setMethod(1)} disabled={!deliveryAllowed} className="mr-3 accent-site-green" />
                <strong>Házhoz szállítás</strong>
                <span className="mt-2 block text-sm text-site-text">{deliveryAllowed ? "A szállítási díj a szolgáltató kiválasztása után véglegesíthető." : "A kosár személyesen átvehető terméket is tartalmaz."}</span>
              </label>
            </div>
          </fieldset>

          <fieldset className="rounded-sm border border-site-border bg-white p-5 shadow-md sm:p-6">
            <legend className="px-2 text-xl font-bold text-site-heading">{method === 1 ? "Szállítási cím" : "Számlázási cím"}</legend>
            {availableAddresses.length ? (
              <div className="mt-2 grid gap-3">
                {availableAddresses.map((address) => <AddressOption key={address.id} address={address} selected={addressId === address.id} onSelect={() => setAddressId(address.id)} />)}
              </div>
            ) : (
              <div className="mt-2 rounded-sm bg-site-surface-soft p-4 text-sm text-site-text">
                Még nincs mentett címed. <Link to={ROUTES.accountAddresses} className="font-semibold text-site-green-dark underline">Cím megadása</Link>
              </div>
            )}
          </fieldset>
        </div>

        <aside className="rounded-sm bg-site-surface-soft p-5 shadow-md lg:sticky lg:top-28">
          <h2 className="text-xl font-bold text-site-heading">Rendelés összesítése</h2>
          <ul className="mt-4 space-y-3 border-b border-site-border pb-4">
            {cart.items.map((item) => (
              <li key={item.id} className="flex justify-between gap-4 text-sm text-site-text">
                <span>{item.productName} × {item.quantity}</span>
                <strong className="whitespace-nowrap text-site-heading">{PRICE.format(item.lineTotal)}</strong>
              </li>
            ))}
          </ul>
          <div className="mt-4 flex justify-between gap-4 text-lg font-bold text-site-heading"><span>Összesen</span><span>{PRICE.format(cart.totalPrice)}</span></div>
          <p className="mt-3 text-xs leading-5 text-site-muted">A demóverzió még nem számol szállítási díjat és nem indít valódi online fizetést.</p>
          <button type="submit" disabled={isSubmitting || !addressId} className="mt-5 w-full rounded-sm bg-site-green px-5 py-3 font-semibold text-white hover:bg-site-green-hover disabled:cursor-not-allowed disabled:opacity-50">
            {isSubmitting ? "Rendelés elküldése..." : "Rendelés elküldése"}
          </button>
        </aside>
      </form>
    </main>
  );
}
