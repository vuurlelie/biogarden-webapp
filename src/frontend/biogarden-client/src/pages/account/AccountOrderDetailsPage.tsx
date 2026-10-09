import { useEffect, useRef, useState } from "react";
import { OrderPurchaseSummary } from "../../features/account/components/OrderPurchaseSummary";
import { FULFILLMENT_LABELS } from "../../features/checkout/constants/fulfillment";
import { Link, useNavigate, useParams } from "react-router";
import {
  cancelOrder,
  getOrderById,
  requestOrderWithdrawal,
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
import { useModalFocus } from "../../shared/hooks/useModalFocus";
import { getErrorMessage, hasErrorStatus } from "../../shared/utils/errorHandling";
import NotFoundPage from "../public/NotFoundPage";

const MAX_PENDING_ORDER_REQUESTS = 3;
const MAX_REQUEST_LENGTH = 1000;

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
  const [withdrawalReason, setWithdrawalReason] = useState("");
  const [isRequestingWithdrawal, setIsRequestingWithdrawal] = useState(false);
  const [withdrawalSuccess, setWithdrawalSuccess] = useState<string | null>(null);
  const [withdrawalValidationError, setWithdrawalValidationError] = useState("");
  const cancellationSuccessModalRef = useRef<HTMLElement | null>(null);

  useModalFocus(
    cancellationSuccessModalRef,
    {
      isOpen: Boolean(cancellationSuccess),
      onClose: () => setCancellationSuccess(null),
    }
  );

  async function handleWithdrawalRequest() {
    if (!order || isRequestingWithdrawal || order.withdrawalRequests.filter(request => request.status === 1).length >= MAX_PENDING_ORDER_REQUESTS)
      return;
    if (withdrawalReason.trim().length < 10) {
      setWithdrawalValidationError("Írd le legalább 10 karakterben a kérésedet.");
      return;
    }

    setIsRequestingWithdrawal(true);
    setError("");
    setWithdrawalValidationError("");
    setWithdrawalSuccess(null);
    try {
      const request = await requestOrderWithdrawal(order.id, withdrawalReason);
      setOrder({
        ...order,
        withdrawalRequests: [request, ...order.withdrawalRequests],
      });
      setWithdrawalReason("");
      setWithdrawalSuccess("A kérésedet rögzítettük, és emailben is visszaigazoltuk.");
    } catch (requestError) {
      setError(getErrorMessage(requestError, "A kérés elküldése nem sikerült."));
    } finally {
      setIsRequestingWithdrawal(false);
    }
  }

  async function handleCancelOrder() {
    if (!order)
      return;
    setIsCancelling(true);
    setError("");
    try {
      const cancelledOrder = await cancelOrder(order.id, cancellationReason);
      setOrder(cancelledOrder);
      setShowCancellation(false);
      setCancellationSuccess(`A(z) ${cancelledOrder.orderNumber} számú rendelést sikeresen lemondtad.`);
    } catch (cancelError) {
      setError(getErrorMessage(cancelError, "A rendelés lemondása nem sikerült."));
    } finally {
      setIsCancelling(false);
    }
  }

  useEffect(
    () => {
      let shouldIgnoreResult = false;

      if (!orderId) {
        setNotFound(true);
        setIsLoading(false);
        return;
      }

      async function loadOrder() {
        try {
          const loadedOrder = await getOrderById(orderId!);
          if (!shouldIgnoreResult)
            setOrder(loadedOrder);
        } catch (loadError) {
          if (shouldIgnoreResult)
            return;
          if (hasErrorStatus(loadError, 401)) {
            navigate(
              ROUTES.login,
              { replace: true }
            );
            return;
          }
          if (hasErrorStatus(loadError, 404)) {
            setNotFound(true);
            return;
          }
          setError(getErrorMessage(loadError, "A rendelés betöltése nem sikerült."));
        } finally {
          if (!shouldIgnoreResult)
            setIsLoading(false);
        }
      }

      void loadOrder();
      return () => {
        shouldIgnoreResult = true;
      };
    },
    [navigate, orderId]
  );

  if (isLoading) {
    return <div className="rounded-sm bg-site-surface-soft p-10 text-center shadow-md">Rendelés betöltése...</div>;
  }

  if (notFound) {
    return <NotFoundPage message="A keresett rendelés nem található vagy nem tartozik ehhez a fiókhoz." />;
  }

  if (!order) {
    return <div className="rounded-sm bg-site-danger-soft p-8 text-center text-site-danger shadow-md">{error || "A rendelés betöltése nem sikerült."}</div>;
  }

  const pendingRequestCount = order.withdrawalRequests.filter((request) => request.status === 1).length;
  const requestLimitReached = pendingRequestCount >= MAX_PENDING_ORDER_REQUESTS;

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
            {FULFILLMENT_LABELS[order.fulfillmentMethod]}
          </dd>
        </div>
      </dl>

      <OrderPurchaseSummary order={order} />
      <section className="mt-6 rounded-sm border border-site-border p-5">
        <h2 className="text-xl font-bold text-site-heading">Bizonylat</h2>
        {order.customerType === 1 ? (
          <div className="mt-3 text-sm leading-6 text-site-text">
            <p>{order.billingCompanyName}</p>
            <p>{order.billingTaxNumber}</p>
            <p>{`${order.postalCode} ${order.city}, ${order.street} ${order.houseNumber}`}</p>
            <p>
              {order.printedInvoiceRequested
                ? "Elektronikus számla és nyomtatott számla"
                : "Elektronikus számla"}
            </p>
          </div>
        ) : (
          <p className="mt-3 text-sm leading-6 text-site-text">
            A rendeléshez papíralapú nyugtát mellékelünk.
          </p>
        )}
      </section>

      <h2 className="mt-8 text-xl font-bold text-site-heading">Termékek</h2>
      <div className="mt-4 space-y-3 sm:hidden">
        {order.items.map((item) => (
          <article key={item.productId} className="rounded-sm border border-site-border bg-white p-4">
            <div className="flex items-start justify-between gap-4">
              <h3 className="min-w-0 text-sm font-bold text-site-heading">{item.productName}</h3>
              <span className="shrink-0 text-sm font-semibold text-site-heading">
                {formatOrderPrice(item.lineTotal)}
              </span>
            </div>
            <dl className="mt-3 grid grid-cols-2 gap-3 text-sm">
              <div>
                <dt className="text-site-muted">Egységár</dt>
                <dd className="mt-1 font-semibold text-site-heading">{formatOrderPrice(item.unitPrice)}</dd>
              </div>
              <div className="text-right">
                <dt className="text-site-muted">Mennyiség</dt>
                <dd className="mt-1 font-semibold text-site-heading">{item.quantity}</dd>
              </div>
            </dl>
          </article>
        ))}
      </div>
      <div className="mt-4 hidden overflow-hidden rounded-sm border border-site-border sm:block">
        <table className="w-full border-collapse text-left text-sm">
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

      <section className="mt-8 rounded-sm border border-site-border p-5">
        <h2 className="text-xl font-bold text-site-heading">Elállási vagy fogyasztói kérés</h2>
        <p className="mt-2 text-sm leading-6 text-site-text">
          Ha a rendeléshez kapcsolódóan elállást, panaszt vagy egyéb fogyasztói kérést szeretnél jelezni,
          itt rögzítheted. A kérés nem automatikus lemondás, a feldolgozásáról külön egyeztetünk.
        </p>
        {order.withdrawalRequests.map(request => (
          <div key={request.id} className={`mt-4 rounded-sm border p-4 text-sm text-site-text ${
            request.status === 1 ? "border-amber-200 bg-amber-50" : "border-site-border bg-stone-50"
          }`}>
            <strong className={`flex items-center gap-2 ${request.status === 1 ? "text-amber-900" : "text-site-green-dark"}`}>
              <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" className="h-5 w-5 shrink-0">
                {request.status === 1 ? <><circle cx="12" cy="12" r="9" /><path d="M12 7v5l3 2" /></>
                  : <><circle cx="12" cy="12" r="9" /><path d="m8 12 3 3 5-6" /></>}
              </svg>
              {request.status === 1 ? "Feldolgozásra váró üzenet" : "Feldolgozott üzenet"}
            </strong>
            <p className="mt-2 whitespace-pre-wrap">{request.reason}</p>
            {request.adminResponse && <div className="mt-3 border-t border-site-border pt-3">
              <strong className="text-site-green-dark">Válaszunk</strong>
              <p className="mt-1 whitespace-pre-wrap">{request.adminResponse}</p>
            </div>}
          </div>
        ))}
        <p className="mt-4 text-sm text-site-muted">
          Feldolgozásra váró üzenetek: {pendingRequestCount} / {MAX_PENDING_ORDER_REQUESTS}.
          {requestLimitReached ? " Újabb üzenetet a korábbiak feldolgozása után küldhetsz." : " További kérdést vagy kiegészítést is küldhetsz."}
        </p>
        {withdrawalSuccess && (
          <div role="status" className="mt-4 rounded-sm bg-site-surface-soft p-3 text-sm font-semibold text-site-green-dark">
            {withdrawalSuccess}
          </div>
        )}
        <label htmlFor="account-withdrawal-reason" className="mt-4 block text-sm font-semibold text-site-heading">
          Kérés leírása
        </label>
          <textarea
            id="account-withdrawal-reason"
            value={withdrawalReason}
            onChange={(event) => {
              setWithdrawalReason(event.target.value);
              if (withdrawalValidationError)
                setWithdrawalValidationError("");
            }}
            disabled={requestLimitReached || isRequestingWithdrawal}
            maxLength={MAX_REQUEST_LENGTH}
            rows={4}
            aria-invalid={Boolean(withdrawalValidationError)}
            aria-describedby={`account-withdrawal-reason-count${withdrawalValidationError ? " account-withdrawal-reason-error" : ""}`}
            className={`mt-2 block w-full rounded-sm border bg-white px-3 py-2.5 disabled:cursor-not-allowed disabled:bg-site-surface-soft ${
              withdrawalValidationError ? "border-site-danger" : "border-site-border"
            }`}
          />
        <div className="mt-1 flex items-baseline justify-between gap-4 text-sm font-normal text-site-muted">
          {withdrawalValidationError && (
            <span id="account-withdrawal-reason-error" className="min-w-0 flex-1 text-xs font-normal text-site-danger">
              {withdrawalValidationError}
            </span>
          )}
          <span id="account-withdrawal-reason-count" className="ml-auto whitespace-nowrap tabular-nums">
            <span aria-hidden="true">{withdrawalReason.trim().length} / {MAX_REQUEST_LENGTH}</span>
            <span className="sr-only">{withdrawalReason.trim().length} karakter a megengedett {MAX_REQUEST_LENGTH} karakterből, a kezdő és záró szóközök nélkül.</span>
          </span>
        </div>
        <button
          type="button"
          onClick={() => void handleWithdrawalRequest()}
          disabled={requestLimitReached || isRequestingWithdrawal}
          className="mt-4 rounded-sm bg-site-green px-5 py-2.5 font-semibold text-white disabled:cursor-not-allowed disabled:opacity-50"
        >
          {requestLimitReached ? "Üzenetküldési korlát elérve" : isRequestingWithdrawal ? "Küldés..." : "Kérés elküldése"}
        </button>
      </section>

      {(order.status === 0 || order.status === 1) && (
        <section className="mt-8 border-t border-site-border pt-6">
          {!showCancellation ? (
            <button
              type="button"
              onClick={() => setShowCancellation(true)}
              aria-expanded={showCancellation}
              aria-controls="account-cancellation-panel"
              className="rounded-sm border border-site-danger px-5 py-2.5 font-semibold text-site-danger hover:bg-site-danger-soft"
            >
              Rendelés lemondása
            </button>
          ) : (
            <div id="account-cancellation-panel" className="rounded-sm bg-site-danger-soft p-5">
              <h2 className="font-bold text-site-danger">Biztosan lemondod a rendelést?</h2>
              <label htmlFor="account-cancellation-reason" className="mt-4 block text-sm font-semibold text-site-heading">
                Lemondás oka (opcionális)
                <textarea
                  id="account-cancellation-reason"
                  value={cancellationReason}
                  onChange={(event) => setCancellationReason(event.target.value)}
                  maxLength={500}
                  rows={3}
                  className="mt-2 block w-full rounded-sm border border-site-border bg-white px-3 py-2.5"
                />
              </label>
              <div className="mt-4 flex flex-wrap gap-3"><button type="button" onClick={() => void handleCancelOrder()} disabled={isCancelling} className="rounded-sm bg-site-danger px-5 py-2.5 font-semibold text-white disabled:opacity-50">{isCancelling ? "Lemondás..." : "Igen, lemondom"}</button><button type="button" onClick={() => setShowCancellation(false)} disabled={isCancelling} className="rounded-sm border border-site-border bg-white px-5 py-2.5">Mégsem</button></div>
            </div>
          )}
        </section>
      )}

      {cancellationSuccess && (
        <div className="fixed inset-0 z-[90] flex items-center justify-center bg-black/50 p-4" role="dialog" aria-modal="true" aria-labelledby="cancellation-success-title">
          <section
            ref={cancellationSuccessModalRef}
            tabIndex={-1}
            className="w-full max-w-md rounded-sm bg-white p-6 text-center shadow-xl outline-none"
          >
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
