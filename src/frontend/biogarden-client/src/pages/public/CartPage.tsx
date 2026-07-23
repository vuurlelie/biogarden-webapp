import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { useCart } from "../../features/cart/context/useCart";
import type { CartItem } from "../../features/cart/types/cart.types";
import { getUnitLabel } from "../../features/products/utils/productLabels";
import { ROUTES } from "../../shared/constants/routes";
import { useAnalytics } from "../../features/analytics/context/AnalyticsContextCore";
import { getCartValueBucket } from "../../features/analytics/utils/eventValidation";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;
const PRICE_FORMATTER = new Intl.NumberFormat("hu-HU", {
  style: "currency",
  currency: "HUF",
  maximumFractionDigits: 0,
});

function formatPrice(value: number): string {
  return PRICE_FORMATTER.format(value);
}

function getImageUrl(image: string | null): string | null {
  return image ? `${API_BASE_URL}/${image.replace(/^\/+/, "")}` : null;
}

function formatRemaining(milliseconds: number): string {
  const seconds = Math.max(0, Math.ceil(milliseconds / 1000));
  const minutes = Math.floor(seconds / 60);
  return `${String(minutes).padStart(2, "0")}:${String(seconds % 60).padStart(2, "0")}`;
}

type CartRowProps = {
  item: CartItem;
  isUpdating: boolean;
  onUpdate: (itemId: string, quantity: number) => Promise<void>;
  onRemove: (itemId: string) => Promise<void>;
};

function CartRow({ item, isUpdating, onUpdate, onRemove }: CartRowProps) {
  const detailsUrl = ROUTES.productDetails.replace(":productId", String(item.productId));
  const imageUrl = getImageUrl(item.image);

  return (
    <article className="grid gap-4 border-b border-site-border py-5 last:border-b-0 sm:grid-cols-[7rem_minmax(0,1fr)] lg:grid-cols-[7rem_minmax(0,1fr)_auto_auto] lg:items-center">
      <Link to={detailsUrl} className="block overflow-hidden rounded-sm bg-site-surface-soft">
        {imageUrl ? (
          <img src={imageUrl} alt={item.productName} className="aspect-square h-full w-full object-cover" />
        ) : (
          <div className="flex aspect-square items-center justify-center text-xs text-site-muted">Nincs kép</div>
        )}
      </Link>

      <div className="min-w-0">
        <h2 className="text-lg font-bold text-site-heading">
          <Link to={detailsUrl} className="hover:text-site-green-dark hover:underline">
            {item.productName}
          </Link>
        </h2>
        <p className="mt-1 text-sm text-site-text">
          {formatPrice(item.unitPrice)} / {getUnitLabel(item.unit)}
        </p>
        <p className="mt-1 text-xs text-site-muted">
          {item.deliveryAvailable ? "Kiszállítható" : "Csak személyes átvétellel"}
        </p>
        {!item.available && <p className="mt-2 text-sm font-semibold text-site-danger">A termék jelenleg nem elérhető.</p>}
      </div>

      <div className="flex items-center gap-2" aria-label={`${item.productName} mennyisége`}>
        <button
          type="button"
          onClick={() => void onUpdate(item.id, item.quantity - 1)}
          disabled={isUpdating}
          className="h-9 w-9 rounded-sm border border-site-border font-bold text-site-heading transition hover:border-site-green-dark hover:text-site-green-dark disabled:cursor-wait disabled:opacity-50"
          aria-label="Mennyiség csökkentése"
        >
          −
        </button>
        <span className="min-w-10 text-center font-semibold tabular-nums" aria-live="polite">
          {item.quantity}
        </span>
        <button
          type="button"
          onClick={() => void onUpdate(item.id, item.quantity + 1)}
          disabled={isUpdating || item.quantity >= item.availableQuantityForCart}
          className="h-9 w-9 rounded-sm border border-site-border font-bold text-site-heading transition hover:border-site-green-dark hover:text-site-green-dark disabled:cursor-not-allowed disabled:opacity-50"
          aria-label="Mennyiség növelése"
        >
          +
        </button>
      </div>

      <div className="flex items-center justify-between gap-5 lg:min-w-36 lg:flex-col lg:items-end lg:gap-2">
        <span className="text-lg font-bold text-site-heading">{formatPrice(item.lineTotal)}</span>
        <button
          type="button"
          onClick={() => void onRemove(item.id)}
          disabled={isUpdating}
          className="text-sm font-semibold text-site-danger hover:underline disabled:cursor-wait disabled:opacity-50"
        >
          Eltávolítás
        </button>
      </div>
    </article>
  );
}

export default function CartPage() {
  const { cart, isLoading, error, refresh, updateQuantity, removeItem, clear } = useCart();
  const [updatingItemId, setUpdatingItemId] = useState<string | null>(null);
  const [isClearing, setIsClearing] = useState(false);
  const [now, setNow] = useState(Date.now());
  const { track } = useAnalytics();

  useEffect(() => {
    void refresh().then(() => setNow(Date.now())).catch(() => undefined);
  }, [refresh]);

  useEffect(() => {
    const interval = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(interval);
  }, []);

  useEffect(() => {
    if (!cart?.items.length) return;
    track("cart_view", {
      itemCount: cart.totalItemCount,
      cartValueBucket: getCartValueBucket(cart.totalPrice),
    });
  }, [cart, track]);

  const remainingMilliseconds = useMemo(
    () => Math.max(0, new Date(cart?.expiresAtUtc ?? 0).getTime() - now),
    [cart?.expiresAtUtc, now],
  );

  async function handleUpdate(itemId: string, quantity: number) {
    setUpdatingItemId(itemId);
    try {
      await updateQuantity(itemId, quantity);
    } catch {
      // The shared cart error is displayed below the page heading.
    } finally {
      setUpdatingItemId(null);
    }
  }

  async function handleRemove(itemId: string) {
    setUpdatingItemId(itemId);
    try {
      await removeItem(itemId);
    } catch {
      // The shared cart error is displayed below the page heading.
    } finally {
      setUpdatingItemId(null);
    }
  }

  async function handleClear() {
    if (!window.confirm("Biztosan kiüríted a kosarat?")) return;
    setIsClearing(true);
    try {
      await clear();
    } catch {
      // The shared cart error is displayed below the page heading.
    } finally {
      setIsClearing(false);
    }
  }

  if (isLoading) {
    return <div className="mx-auto max-w-7xl px-4 py-12 text-center text-site-text">A kosár betöltése...</div>;
  }

  return (
    <main className="mx-auto w-full max-w-7xl px-4 py-6 sm:px-6 lg:px-8">
      <nav className="mb-5 flex items-center gap-2 text-sm text-site-text" aria-label="Morzsamenü">
        <Link to={ROUTES.home} className="font-bold text-site-green-dark hover:underline">Kezdőlap</Link>
        <span aria-hidden="true">/</span>
        <span aria-current="page">Kosár</span>
      </nav>

      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold text-site-heading sm:text-4xl">Kosár</h1>
          {cart && cart.items.length > 0 && (
            <p className="mt-2 text-sm text-site-text">
              A készletfoglalásból hátralévő idő: <strong className="tabular-nums text-site-green-dark">{formatRemaining(remainingMilliseconds)}</strong>
            </p>
          )}
        </div>
        {cart && cart.items.length > 0 && (
          <button type="button" onClick={() => void handleClear()} disabled={isClearing} className="text-sm font-semibold text-site-danger hover:underline disabled:opacity-50">
            {isClearing ? "Kosár ürítése..." : "Kosár kiürítése"}
          </button>
        )}
      </div>

      {error && (
        <div role="alert" className="mt-5 flex flex-wrap items-center justify-between gap-3 rounded-sm bg-site-danger-soft p-4 text-site-danger">
          <span>{error}</span>
          <button type="button" onClick={() => void refresh()} className="font-semibold underline">Újrapróbálom</button>
        </div>
      )}

      {!cart || cart.items.length === 0 ? (
        <section className="mt-7 rounded-sm border border-site-border bg-site-surface-soft px-5 py-14 text-center shadow-sm">
          <h2 className="text-2xl font-bold text-site-heading">A kosarad még üres</h2>
          <p className="mx-auto mt-3 max-w-lg text-site-text">Nézd meg a kert aktuális kínálatát, és tedd kosárba a kiválasztott termékeket.</p>
          <Link to={ROUTES.products} className="mt-6 inline-flex rounded-sm bg-site-green px-5 py-3 font-semibold text-white transition-colors hover:bg-site-green-hover">
            Termékek megtekintése
          </Link>
        </section>
      ) : (
        <div className="mt-7 grid items-start gap-7 lg:grid-cols-[minmax(0,1fr)_22rem]">
          <section className="rounded-sm border border-site-border bg-white px-4 shadow-md sm:px-6" aria-label="Kosár tartalma">
            {cart.items.map((item) => (
              <CartRow key={item.id} item={item} isUpdating={updatingItemId === item.id} onUpdate={handleUpdate} onRemove={handleRemove} />
            ))}
          </section>

          <aside className="rounded-sm bg-site-surface-soft p-5 shadow-md lg:sticky lg:top-28">
            <h2 className="text-xl font-bold text-site-heading">Összesítés</h2>
            <dl className="mt-5 space-y-3 text-site-text">
              <div className="flex justify-between gap-4"><dt>Termékek száma</dt><dd className="font-semibold">{cart.totalItemCount} db</dd></div>
              <div className="flex justify-between gap-4 border-t border-site-border pt-4 text-lg"><dt className="font-bold text-site-heading">Összesen</dt><dd className="font-bold text-site-heading">{formatPrice(cart.totalPrice)}</dd></div>
            </dl>
            <p className="mt-4 text-xs leading-5 text-site-muted">A következő lépésben megadhatod a kapcsolattartási és számlázási adatokat, valamint kiválaszthatod az átvételi vagy szállítási módot.</p>
            <Link to={ROUTES.checkout} className="mt-5 flex w-full justify-center rounded-sm bg-site-green px-5 py-3 font-semibold text-white transition-colors hover:bg-site-green-hover">
              Személyes és szállítási adatok megadása
            </Link>
            <Link to={ROUTES.products} className="mt-3 flex w-full justify-center rounded-sm border border-site-green-dark px-5 py-2.5 text-sm font-semibold text-site-green-dark transition-colors hover:bg-site-green-dark hover:text-white">
              Vásárlás folytatása
            </Link>
          </aside>
        </div>
      )}
    </main>
  );
}
