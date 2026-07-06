import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import {
  getOrderById,
  OrdersApiError,
} from "../../features/account/api/ordersApi";
import type { Order } from "../../features/account/types/order.types";
import {
  formatOrderDate,
  formatOrderPrice,
  getOrderStatusClass,
  getOrderStatusLabel,
  getPaymentStatusLabel,
} from "../../features/account/utils/orderLabels";
import { ROUTES } from "../../shared/constants/routes";
import NotFoundPage from "../public/NotFoundPage";

export default function AccountOrderDetailsPage() {
  const { orderId } = useParams<{ orderId: string }>();
  const navigate = useNavigate();
  const [order, setOrder] = useState<Order | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState("");
  const [notFound, setNotFound] = useState(false);

  useEffect(() => {
    let shouldIgnoreResult = false;

    if (!orderId) {
      setNotFound(true);
      setIsLoading(false);
      return;
    }

    async function loadOrder() {
      try {
        const loadedOrder = await getOrderById(orderId!);
        if (!shouldIgnoreResult) setOrder(loadedOrder);
      } catch (loadError) {
        if (shouldIgnoreResult) return;
        if (loadError instanceof OrdersApiError) {
          if (loadError.status === 401) {
            navigate(ROUTES.login, { replace: true });
            return;
          }
          if (loadError.status === 404) {
            setNotFound(true);
            return;
          }
        }
        setError(loadError instanceof Error ? loadError.message : "A rendelés betöltése nem sikerült.");
      } finally {
        if (!shouldIgnoreResult) setIsLoading(false);
      }
    }

    void loadOrder();
    return () => {
      shouldIgnoreResult = true;
    };
  }, [navigate, orderId]);

  if (isLoading) {
    return <div className="rounded-sm bg-site-surface-soft p-10 text-center shadow-md">Rendelés betöltése...</div>;
  }

  if (notFound) {
    return <NotFoundPage message="A keresett rendelés nem található vagy nem tartozik ehhez a fiókhoz." />;
  }

  if (error || !order) {
    return <div className="rounded-sm bg-site-danger-soft p-8 text-center text-site-danger shadow-md">{error || "A rendelés betöltése nem sikerült."}</div>;
  }

  return (
    <div className="rounded-sm border border-site-border bg-white p-6 shadow-lg sm:p-8">
      <Link to={ROUTES.accountOrders} className="text-sm font-semibold text-site-green-dark hover:underline">← Vissza a rendelésekhez</Link>

      <div className="mt-5 flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <p className="text-xs font-bold uppercase tracking-[0.15em] text-site-green-dark">Rendelés részletei</p>
          <h1 className="mt-2 text-3xl font-bold text-site-heading">{order.orderNumber}</h1>
          <p className="mt-2 text-sm text-site-muted">Leadva: {formatOrderDate(order.createdAtUtc)}</p>
        </div>
        <span className={`w-fit rounded-full px-3 py-1 text-xs font-bold ${getOrderStatusClass(order.status)}`}>
          {getOrderStatusLabel(order.status)}
        </span>
      </div>

      <dl className="mt-7 grid gap-4 rounded-sm bg-site-surface-soft p-5 sm:grid-cols-2">
        <div>
          <dt className="text-sm font-bold text-site-heading">Fizetés állapota</dt>
          <dd className="mt-1 text-sm text-site-text">{getPaymentStatusLabel(order.paymentStatus)}</dd>
        </div>
        <div>
          <dt className="text-sm font-bold text-site-heading">Végösszeg</dt>
          <dd className="mt-1 text-lg font-bold text-site-heading">{formatOrderPrice(order.totalPrice)}</dd>
        </div>
      </dl>

      <h2 className="mt-8 text-xl font-bold text-site-heading">Termékek</h2>
      <div className="mt-4 overflow-x-auto rounded-sm border border-site-border">
        <table className="w-full min-w-[620px] border-collapse text-left text-sm">
          <thead className="bg-site-surface-soft text-site-heading">
            <tr>
              <th className="px-4 py-3 font-bold">Termék</th>
              <th className="px-4 py-3 text-right font-bold">Egységár</th>
              <th className="px-4 py-3 text-right font-bold">Mennyiség</th>
              <th className="px-4 py-3 text-right font-bold">Összesen</th>
            </tr>
          </thead>
          <tbody>
            {order.items.map((item) => (
              <tr key={item.productId} className="border-t border-site-border">
                <td className="px-4 py-3 font-semibold text-site-heading">{item.productName}</td>
                <td className="px-4 py-3 text-right">{formatOrderPrice(item.unitPrice)}</td>
                <td className="px-4 py-3 text-right">{item.quantity}</td>
                <td className="px-4 py-3 text-right font-semibold text-site-heading">{formatOrderPrice(item.lineTotal)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {order.status === 4 && order.cancellationReason && (
        <div className="mt-6 rounded-sm bg-site-danger-soft p-4 text-sm text-site-danger">
          <span className="font-bold">Törlés oka:</span> {order.cancellationReason}
        </div>
      )}
    </div>
  );
}

