import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import {
  cancelOrder,
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
  const [showCancellation, setShowCancellation] = useState(false);
  const [cancellationReason, setCancellationReason] = useState("");
  const [isCancelling, setIsCancelling] = useState(false);
  const [cancellationSuccess, setCancellationSuccess] = useState<string | null>(null);

  async function handleCancelOrder() {
    if (!order) return;
    setIsCancelling(true);
    setError("");
    try {
      const cancelledOrder = await cancelOrder(order.id, cancellationReason);
      setOrder(cancelledOrder);
      setShowCancellation(false);
      setCancellationSuccess(`A(z) ${cancelledOrder.orderNumber} számú rendelést sikeresen lemondtad.`);
    } catch (cancelError) {
      setError(cancelError instanceof Error ? cancelError.message : "A rendelés lemondása nem sikerült.");
    } finally {
      setIsCancelling(false);
    }
  }

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

  if (!order) {
    return <div className="rounded-sm bg-site-danger-soft p-8 text-center text-site-danger shadow-md">{error || "A rendelés betöltése nem sikerült."}</div>;
  }

  return (
    <div className="rounded-sm border border-site-border bg-white p-6 shadow-lg sm:p-8">
      <Link to={ROUTES.accountOrders} className="text-sm font-semibold text-site-green-dark hover:underline">← Vissza a rendelésekhez</Link>
      {error && <div role="alert" className="mt-5 rounded-sm bg-site-danger-soft p-4 text-site-danger">{error}</div>}

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

      <dl className="mt-7 grid gap-4 rounded-sm bg-site-surface-soft p-5 sm:grid-cols-3">
        <div>
          <dt className="text-sm font-bold text-site-heading">Fizetés állapota</dt>
          <dd className="mt-1 text-sm text-site-text">{getPaymentStatusLabel(order.paymentStatus)}</dd>
        </div>
        <div>
          <dt className="text-sm font-bold text-site-heading">Végösszeg</dt>
          <dd className="mt-1 text-lg font-bold text-site-heading">{formatOrderPrice(order.totalPrice)}</dd>
        </div>
        <div>
          <dt className="text-sm font-bold text-site-heading">Átvételi mód</dt>
          <dd className="mt-1 text-sm text-site-text">
            {order.fulfillmentMethod === 1 ? "Házhoz szállítás" : "Személyes átvétel"}
          </dd>
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

      {(order.status === 0 || order.status === 1) && (
        <section className="mt-8 border-t border-site-border pt-6">
          {!showCancellation ? (
            <button type="button" onClick={() => setShowCancellation(true)} className="rounded-sm border border-site-danger px-5 py-2.5 font-semibold text-site-danger hover:bg-site-danger-soft">Rendelés lemondása</button>
          ) : (
            <div className="rounded-sm bg-site-danger-soft p-5">
              <h2 className="font-bold text-site-danger">Biztosan lemondod a rendelést?</h2>
              <label className="mt-4 block text-sm font-semibold text-site-heading">Lemondás oka (opcionális)<textarea value={cancellationReason} onChange={(event) => setCancellationReason(event.target.value)} maxLength={500} rows={3} className="mt-2 block w-full rounded-sm border border-site-border bg-white px-3 py-2.5" /></label>
              <div className="mt-4 flex flex-wrap gap-3"><button type="button" onClick={() => void handleCancelOrder()} disabled={isCancelling} className="rounded-sm bg-site-danger px-5 py-2.5 font-semibold text-white disabled:opacity-50">{isCancelling ? "Lemondás..." : "Igen, lemondom"}</button><button type="button" onClick={() => setShowCancellation(false)} disabled={isCancelling} className="rounded-sm border border-site-border bg-white px-5 py-2.5">Mégsem</button></div>
            </div>
          )}
        </section>
      )}

      {cancellationSuccess && (
        <div className="fixed inset-0 z-[90] flex items-center justify-center bg-black/50 p-4" role="dialog" aria-modal="true" aria-labelledby="cancellation-success-title">
          <section className="w-full max-w-md rounded-sm bg-white p-6 text-center shadow-xl">
            <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-site-surface-soft text-2xl text-site-green-dark">✓</div>
            <h2 id="cancellation-success-title" className="mt-4 text-2xl font-bold text-site-heading">Sikeres lemondás</h2>
            <p className="mt-3 text-site-text">{cancellationSuccess}</p>
            <button type="button" onClick={() => setCancellationSuccess(null)} className="mt-6 rounded-sm bg-site-green px-6 py-3 font-semibold text-white hover:bg-site-green-hover">Rendben</button>
          </section>
        </div>
      )}
    </div>
  );
}

