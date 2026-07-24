import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
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

export default function AccountOrdersPage() {
  const handleAccountError = useAccountErrorHandler();
  const [orders, setOrders] = useState<Order[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    let shouldIgnoreResult = false;

    async function loadOrders() {
      try {
        const loadedOrders = await getOrders();
        if (!shouldIgnoreResult) setOrders(loadedOrders);
      } catch (loadError) {
        if (shouldIgnoreResult) return;
        setError(handleAccountError(loadError, "A rendelések betöltése nem sikerült.") ?? "");
      } finally {
        if (!shouldIgnoreResult) setIsLoading(false);
      }
    }

    void loadOrders();
    return () => {
      shouldIgnoreResult = true;
    };
  }, [handleAccountError]);

  if (isLoading) {
    return <AccountLoadingPanel label="Rendelések betöltése..." />;
  }

  return (
    <AccountPageShell
      title="Rendeléseim"
      description="Korábbi rendeléseid és azok aktuális állapota."
    >
      <AccountFeedback error={error} success="" />

      {!error && orders.length === 0 && (
        <div className="mt-8 rounded-sm bg-site-surface-soft p-8 text-center">
          <h2 className="text-xl font-bold text-site-heading">Még nincs rendelésed</h2>
          <p className="mt-2 text-sm text-site-text">Az első rendelésed után itt követheted majd annak állapotát.</p>
          <Link to={ROUTES.products} className="mt-5 inline-flex rounded-sm bg-site-green px-5 py-2.5 font-semibold text-white hover:bg-site-green-hover">
            Termékek megtekintése
          </Link>
        </div>
      )}

      {orders.length > 0 && (
        <div className="mt-7 space-y-4">
          {orders.map((order) => (
            <article key={order.id} className="rounded-sm border border-site-border p-5 transition hover:border-site-green hover:shadow-md">
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
                  className="font-semibold text-site-green-dark hover:underline"
                >
                  Részletek →
                </Link>
              </div>
            </article>
          ))}
        </div>
      )}
    </AccountPageShell>
  );
}

