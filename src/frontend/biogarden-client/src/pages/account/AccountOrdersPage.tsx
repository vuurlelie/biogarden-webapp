import AdminPagination from "../../shared/components/admin/AdminPagination";
import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router";
import {
  getOrders,
} from "../../features/account/api/ordersApi";
import AccountFeedback from "../../features/account/components/AccountFeedback";
import AccountLoadingPanel from "../../features/account/components/AccountLoadingPanel";
import AccountPageShell from "../../features/account/components/AccountPageShell";
import { useAccountErrorHandler } from "../../features/account/hooks/useAccountErrorHandler";
import type { Order } from "../../features/account/types/order.types";
import {
  formatOrderDate,
  formatOrderPrice,
  getOrderStatusClass,
  getOrderStatusLabel,
  getPaymentStatusLabel,
} from "../../features/account/utils/orderLabels";
import { ROUTES } from "../../shared/constants/routes";

const orderFilterInputClassName = "mt-1 block h-11 w-full rounded-sm border border-site-border px-3 py-2 text-sm font-normal";

function OrderYearFilter({ year, onApply }: { year: string; onApply: (value: string) => void }) {
  const [value, setValue] = useState(year);
  const [error, setError] = useState("");
  function apply() {
    if (value && (!Number.isInteger(Number(value)) || Number(value) < 2000 || Number(value) > 9998)) {
      setError("Adj meg egy érvényes, négyjegyű évet.");
      return;
    }
    setError("");
    onApply(value);
  }
  return <div>
    <label htmlFor="order-year" className="block text-sm font-semibold">Rendelés éve</label>
    <input id="order-year" type="number" min={2000} max={9998} placeholder="Minden év" value={value}
      onChange={event => { setValue(event.target.value); setError(""); }} onBlur={apply}
      onKeyDown={event => { if (event.key === "Enter") { event.preventDefault(); apply(); } }}
      aria-invalid={Boolean(error)} aria-describedby={error ? "order-year-error" : undefined}
      className={orderFilterInputClassName} />
    {error && <p id="order-year-error" role="alert" className="mt-1 text-xs font-normal text-site-danger">{error}</p>}
  </div>;
}

export default function AccountOrdersPage() {
  const [params, setParams] = useSearchParams();
  const view = params.get("view") === "history" ? "history" : "recent";
  const parsedPage = Number(params.get("page") ?? 1);
  const page = Number.isSafeInteger(parsedPage) && parsedPage > 0 ? parsedPage : 1;
  const search = params.get("search") ?? "";
  const year = params.get("year") ?? "";
  const [totalPages, setTotalPages] = useState(1);
  function changeFilter(key: string, value: string) {
    setParams(current => { const next = new URLSearchParams(current); if (value) next.set(key, value); else next.delete(key); next.delete("page"); return next; });
  }
  const handleAccountError = useAccountErrorHandler();
  const [orders, setOrders] = useState<Order[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    let shouldIgnoreResult = false;

    async function loadOrders() {
      try {
        setIsLoading(true);
        setError("");
        const loadedOrders = await getOrders({ view, page, search, year });
        if (!shouldIgnoreResult) {
          setOrders(loadedOrders.items);
          setTotalPages(loadedOrders.totalPages);
          if (loadedOrders.page !== page) setParams(current => { const next = new URLSearchParams(current); next.set("page", String(loadedOrders.page)); return next; }, { replace: true });
        }
      } catch (loadError) {
        if (shouldIgnoreResult) return;
        setError(handleAccountError(loadError, "A rendelések betöltése nem sikerült.") ?? "");
      } finally {
        if (!shouldIgnoreResult) setIsLoading(false);
      }
    }

    const timer = window.setTimeout(() => void loadOrders(), search ? 250 : 0);
    return () => {
      shouldIgnoreResult = true;
      window.clearTimeout(timer);
    };
  }, [handleAccountError, view, page, search, year, setParams]);


  return (
    <AccountPageShell
      title="Rendeléseim"
      description="Korábbi rendeléseid és azok aktuális állapota."
    >
      <div className="mt-6 flex flex-wrap gap-3" aria-label="Rendelések nézete">
        <button type="button" aria-pressed={view === "recent"} onClick={() => changeFilter("view", "recent")} className={`rounded-sm border px-4 py-2 ${view === "recent" ? "bg-site-green text-white" : "border-site-border"}`}>Aktuális rendelések</button>
        <button type="button" aria-pressed={view === "history"} onClick={() => changeFilter("view", "history")} className={`rounded-sm border px-4 py-2 ${view === "history" ? "bg-site-green text-white" : "border-site-border"}`}>Korábbi rendelések</button>
      </div>
      <div className="mt-4 grid gap-4 sm:grid-cols-2">
        <div>
          <label htmlFor="order-number" className="block text-sm font-semibold">Rendelésszám</label>
          <input id="order-number" maxLength={32} value={search} onChange={event => changeFilter("search", event.target.value)} className={orderFilterInputClassName} />
        </div>
        <OrderYearFilter key={year} year={year} onApply={value => changeFilter("year", value)} />
      </div>
      <AccountFeedback error={error} success="" />
      {isLoading && <AccountLoadingPanel label="Rendelések betöltése..." />}

      {!isLoading && !error && orders.length === 0 && (
        <div className="mt-8 rounded-sm bg-site-surface-soft p-8 text-center">
          <h2 className="text-xl font-bold text-site-heading">Nincs megjeleníthető rendelés</h2>
          <p className="mt-2 text-sm text-site-text">Ebben a nézetben a megadott szűrőkkel nem található rendelés.</p>
          <Link to={ROUTES.products} className="mt-5 inline-flex rounded-sm bg-site-green px-5 py-2.5 font-semibold text-white hover:bg-site-green-hover">
            Termékek megtekintése
          </Link>
        </div>
      )}

      {!isLoading && !error && orders.length > 0 && (
        <div className="mt-7 space-y-4">
          {orders.map((order) => (
            <article key={order.id} className="relative rounded-sm border border-site-border p-5 transition hover:border-site-green hover:shadow-md focus-within:ring-2 focus-within:ring-site-green-dark focus-within:ring-offset-2">
              <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                <div>
                  <p className="text-sm text-site-muted">{formatOrderDate(order.createdAtUtc)}</p>
                  <h2 className="mt-1 text-lg font-bold text-site-heading">{order.orderNumber}</h2>
                  <p className="mt-2 text-sm text-site-text">{order.items.length} tétel · {getPaymentStatusLabel(order.paymentStatus)}</p>
                </div>
                <div className="flex flex-wrap items-center gap-3 sm:justify-end">
                  <span className={`rounded-full px-3 py-1 text-xs font-bold ${getOrderStatusClass(order.status)}`}>
                    {getOrderStatusLabel(order.status)}
                  </span>
                  <span className="text-lg font-bold text-site-heading">{formatOrderPrice(order.totalPrice)}</span>
                </div>
              </div>
              <div className="mt-4 border-t border-site-border pt-4 text-right">
                <Link
                  to={ROUTES.accountOrderDetails.replace(":orderId", order.id)}
                  aria-label={`Részletek – ${order.orderNumber}`}
                  className="font-semibold text-site-green-dark hover:underline after:absolute after:inset-0 after:rounded-sm"
                >
                  Részletek →
                </Link>
              </div>
            </article>
          ))}
        </div>
      )}
      {!isLoading && !error && <AdminPagination page={page} totalPages={totalPages} onPageChange={nextPage => setParams(current => { const next = new URLSearchParams(current); next.set("page", String(nextPage)); return next; })} />}
    </AccountPageShell>
  );
}

