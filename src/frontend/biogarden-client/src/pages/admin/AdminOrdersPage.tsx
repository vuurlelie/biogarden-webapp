import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { useSearchParams } from "react-router";
import { OrderPurchaseSummary } from "../../features/account/components/OrderPurchaseSummary";
import { FULFILLMENT_LABELS } from "../../features/checkout/constants/fulfillment";
import type {
  FulfillmentMethod,
  Order,
  OrderStatus,
  PaymentStatus,
} from "../../features/account/types/order.types";
import {
  formatOrderDate,
  formatOrderPrice,
  getOrderStatusClass,
  getOrderStatusLabel,
  getPaymentStatusLabel,
} from "../../features/account/utils/orderLabels";
import { getAdminOrders, updateAdminOrder } from "../../features/admin/api/adminOrdersApi";
import { manualPaymentChoices, PAYMENT_STATUS, ORDER_STATUS } from "../../features/admin/utils/paymentTransitions";
import { getUnitLabel } from "../../features/products/utils/productLabels";
import AdminFeedbackMessages from "../../shared/components/admin/AdminFeedbackMessages";
import AdminListEmptyState from "../../shared/components/admin/AdminListEmptyState";
import AdminListSkeletonGrid from "../../shared/components/admin/AdminListSkeletonGrid";
import AdminPageHeader from "../../shared/components/admin/AdminPageHeader";
import AdminPagination from "../../shared/components/admin/AdminPagination";
import { getErrorMessage, isAbortError, hasErrorStatus } from "../../shared/utils/errorHandling";

const PAGE_SIZE = 12;

const fulfillmentLabels = FULFILLMENT_LABELS;

const statuses: OrderStatus[] = [0, 1, 2, 3, 4];

function address(
  postal: string | null,
  city: string | null,
  street: string | null,
  house: string | null,
) {
  return [postal, city, street, house].filter(Boolean).join(" ") || "Nincs megadva";
}

function DetailCard({
  title,
  children,
}: {
  title: string;
  children: ReactNode;
}) {
  return (
    <section className="min-w-0 rounded-sm border border-site-border p-4">
      <h3 className="font-bold text-site-heading">{title}</h3>
      <div className="mt-2 min-w-0 break-words text-sm leading-6 text-site-text">{children}</div>
    </section>
  );
}

function OrderCard({
  order,
  onUpdated,
}: {
  order: Order;
  onUpdated: (order: Order) => void;
}) {
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState(false);
  const [status, setStatus] = useState<OrderStatus>(order.status);
  const [payment, setPayment] = useState<PaymentStatus>(order.paymentStatus);
  const [reason, setReason] = useState(order.cancellationReason ?? "");
  const [paymentReason, setPaymentReason] = useState("");
  const paymentChanged = payment !== order.paymentStatus || (status === ORDER_STATUS.Cancelled && order.status !== ORDER_STATUS.Cancelled &&
    (order.paymentStatus === PAYMENT_STATUS.Paid || order.paymentStatus === PAYMENT_STATUS.PartiallyRefunded));
  const allowedPayments = manualPaymentChoices(order);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);
  const [previousInput, setPreviousInput] = useState("");
  async function reloadCurrent() {
    setSaving(true);
    try {
      const current = (await getAdminOrders()).find(value => value.id === order.id);
      if (!current) throw new Error("A rendelés már nem érhető el.");
      setPreviousInput(`Korábbi bevitel: ${getOrderStatusLabel(status)}, ${getPaymentStatusLabel(payment)}; ${reason}; ${paymentReason}. Ellenőrizd az aktuális adatokat, majd alkalmazd újra a szükséges módosítást.`);
      onUpdated(current); setStatus(current.status); setPayment(current.paymentStatus);
      setReason(current.cancellationReason ?? ""); setConflict(false); setError(null);
      setPaymentReason("");
    } catch (error) { setError(getErrorMessage(error, "A frissítés nem sikerült.")); }
    finally { setSaving(false); }
  }
  const terminal = order.status === 3 || order.status === 4;
  const detailsId = `admin-order-details-${order.id}`;
  const statusSelectId = `admin-order-status-${order.id}`;
  const paymentSelectId = `admin-order-payment-${order.id}`;
  const cancellationReasonId = `admin-order-cancellation-reason-${order.id}`;

  async function save() {
    if (saving || conflict) return;
    if (paymentChanged && !paymentReason.trim()) { setError("A fizetési módosítás indoklása kötelező."); return; }
    setSaving(true);
    setError(null);

    try {
      const updated = await updateAdminOrder(
        order.id,
        status,
        payment,
        status === 4 ? reason.trim() || null : null,
        order.version,
        paymentReason.trim() || null,
      );
      onUpdated(updated);
      setStatus(updated.status); setPayment(updated.paymentStatus);
      setReason(updated.cancellationReason ?? ""); setPaymentReason("");
      setEditing(false);
    } catch (saveError) {
      if (hasErrorStatus(saveError, 409)) setConflict(true);
      setError(getErrorMessage(saveError, "A módosítás nem sikerült."));
    } finally {
      setSaving(false);
    }
  }

  function cancelEdit() {
    setStatus(order.status);
    setPayment(order.paymentStatus);
    setReason(order.cancellationReason ?? "");
    setPaymentReason("");
    setError(null);
    setEditing(false);
  }

  return (
    <article className="min-w-0 rounded-sm border border-site-border bg-white p-5 shadow-sm">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <h2 className="min-w-0 break-words text-lg font-bold text-site-heading">
              {order.orderNumber}
            </h2>
            <span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${getOrderStatusClass(order.status)}`}>
              {getOrderStatusLabel(order.status)}
            </span>
          </div>
          <p className="mt-1 break-words text-sm text-site-muted">
            {formatOrderDate(order.createdAtUtc)} · {order.customerName}
          </p>
        </div>
        <strong className="min-w-0 break-words text-xl text-site-heading sm:text-right">
          {formatOrderPrice(order.totalPrice)}
        </strong>
      </div>

      <dl className="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-4">
        <div className="min-w-0">
          <dt className="text-xs text-site-muted">Tételek</dt>
          <dd className="mt-1 font-semibold">{order.items.reduce((sum, item) => sum + item.quantity, 0)} db</dd>
        </div>
        <div className="min-w-0">
          <dt className="text-xs text-site-muted">Átvétel</dt>
          <dd className="mt-1 break-words font-semibold">{fulfillmentLabels[order.fulfillmentMethod]}</dd>
        </div>
        <div className="min-w-0">
          <dt className="text-xs text-site-muted">Fizetés</dt>
          <dd className="mt-1 break-words font-semibold">{getPaymentStatusLabel(order.paymentStatus)}</dd>
        </div>
        <div className="min-w-0">
          <dt className="text-xs text-site-muted">Fiók</dt>
          <dd className="mt-1 font-semibold">{order.userId ? "Regisztrált" : "Vendég"}</dd>
        </div>
      </dl>

      {previousInput && <p role="status">{previousInput}</p>}
      {conflict && <button type="button" disabled={saving} onClick={() => void reloadCurrent()}>Aktuális adatok betöltése</button>}
      {error && (
        <p role="alert" className="mt-4 rounded-sm bg-site-danger-soft p-3 text-sm text-site-danger">
          {error}
        </p>
      )}

      {open && (
        <div id={detailsId} className="mt-5 min-w-0 space-y-5 border-t border-site-border pt-5">
          <OrderPurchaseSummary order={order} />
          <section className="min-w-0">
            <h3 className="font-bold text-site-heading">Termékek</h3>
            <div className="mt-2 space-y-2">
              {order.items.map((item) => (
                <div
                  key={`${order.id}-${item.productId}`}
                  className="grid min-w-0 gap-1 rounded-sm bg-site-surface-soft p-3 text-sm sm:grid-cols-[minmax(10rem,1fr)_minmax(7rem,8rem)_minmax(5.5rem,6.5rem)] sm:gap-4"
                >
                  <span className="min-w-0 break-words">{item.productName} × {item.quantity}</span>
                  <span className="min-w-0 break-words text-site-muted">
                    {formatOrderPrice(item.unitPrice)} / {getUnitLabel(item.unit)}
                  </span>
                  <strong className="min-w-0 break-words sm:text-right">{formatOrderPrice(item.lineTotal)}</strong>
                </div>
              ))}
            </div>
          </section>

          <div className="grid min-w-0 gap-4 lg:grid-cols-3">
            <DetailCard title="Vevő">
              <p>{order.customerName}</p>
              <p className="break-all">{order.customerEmail}</p>
              <p className="break-words">{order.customerPhone}</p>
            </DetailCard>

            <DetailCard title="Kiállítandó bizonylat">
              {order.customerType === 1 ? (
                <>
                  <p className="font-semibold">
                    {order.printedInvoiceRequested
                      ? "Elektronikus számla + nyomtatott számla"
                      : "Elektronikus számla"}
                  </p>
                  <p>
                    {order.billingCompanyName}
                    <br />
                    {order.billingTaxNumber}
                  </p>
                  <p>{address(order.postalCode, order.city, order.street, order.houseNumber)}</p>
                </>
              ) : (
                <p className="font-semibold">Papíralapú nyugta a rendelés mellé.</p>
              )}
            </DetailCard>

              <DetailCard title="Szállítási cím">
              <p>
                {order.fulfillmentMethod === 0
                  ? "Személyes átvétel"
                  : address(
                      order.deliveryPostalCode,
                      order.deliveryCity,
                      order.deliveryStreet,
                      order.deliveryHouseNumber,
                    )}
              </p>
            </DetailCard>
          </div>

          {order.cancellationReason && (
            <p className="break-words rounded-sm bg-site-danger-soft p-3 text-sm text-site-danger">
              <strong>Lemondás oka:</strong> {order.cancellationReason}
            </p>
          )}

          {order.withdrawalRequests.length > 0 && (
            <section className="rounded-sm border border-site-green/30 bg-site-surface-soft p-4">
              <h3 className="font-bold text-site-heading">Elállási vagy fogyasztói kérések</h3>
              <div className="mt-3 space-y-3">
                {order.withdrawalRequests.map((request) => (
                  <article key={request.id} className="rounded-sm bg-white p-3 text-sm text-site-text">
                    <p className="font-semibold">
                      {request.status === 1 ? "Folyamatban" : "Feldolgozva"} · {formatOrderDate(request.requestedAtUtc)}
                    </p>
                    <p className="mt-2 break-words">{request.reason}</p>
                  </article>
                ))}
              </div>
            </section>
          )}

          {editing && (
            <section className="rounded-sm border border-site-green/30 bg-site-surface-soft p-4">
              <h3 className="font-bold text-site-heading">Rendelés módosítása</h3>
              <div className="mt-3 grid gap-4 sm:grid-cols-2">
                <label htmlFor={statusSelectId} className="text-sm font-semibold">
                  Rendelés állapota
                  <select
                    id={statusSelectId}
                    value={status}
                    disabled={terminal}
                    onChange={(event) => setStatus(Number(event.target.value) as OrderStatus)}
                    className="mt-1 w-full rounded-sm border border-site-border bg-white px-3 py-2.5"
                  >
                    {statuses
                      .filter((value) => value === order.status || value >= order.status)
                      .map((value) => (
                        <option key={value} value={value}>{getOrderStatusLabel(value)}</option>
                      ))}
                  </select>
                </label>

                <label htmlFor={paymentSelectId} className="text-sm font-semibold">
                  Fizetés állapota
                  <select
                    id={paymentSelectId}
                    value={payment}
                    onChange={(event) => setPayment(Number(event.target.value) as PaymentStatus)}
                    className="mt-1 w-full rounded-sm border border-site-border bg-white px-3 py-2.5"
                  >
                    {allowedPayments.map((value) => (
                      <option key={value} value={value}>{getPaymentStatusLabel(value)}</option>
                    ))}
                  </select>
                </label>
              </div>

              {paymentChanged && <label className="mt-4 block text-sm font-semibold">Fizetési módosítás indoklása
                <textarea value={paymentReason} onChange={event => setPaymentReason(event.target.value)} maxLength={500} className="mt-1 w-full rounded-sm border p-2" />
                <span className="block text-sm">A visszatérítést külön kell elvégezni a szolgáltatónál; itt csak a megtörtént művelet állapotát rögzíted.</span>
              </label>}
              {status === 4 && (
                <label htmlFor={cancellationReasonId} className="mt-4 block text-sm font-semibold">
                  Lemondás oka
                  <textarea
                    id={cancellationReasonId}
                    value={reason}
                    onChange={(event) => setReason(event.target.value)}
                    maxLength={1000}
                    className="mt-1 min-h-20 w-full rounded-sm border border-site-border bg-white px-3 py-2.5"
                  />
                </label>
              )}

              <div className="mt-4 flex flex-col-reverse gap-3 sm:flex-row">
                <button
                  type="button"
                  onClick={cancelEdit}
                  disabled={saving}
                  className="rounded-sm border border-site-border px-4 py-2.5 font-semibold"
                >
                  Mégse
                </button>
                <button
                  type="button"
                  onClick={() => void save()}
                  disabled={saving || conflict}
                  className="rounded-sm bg-site-green px-4 py-2.5 font-semibold text-white disabled:opacity-60"
                >
                  {saving ? "Mentés..." : "Mentés"}
                </button>
              </div>
            </section>
          )}
        </div>
      )}

      <div className="mt-5 flex flex-wrap gap-3 border-t border-site-border pt-4">
        <button
          type="button"
          onClick={() => setOpen((value) => !value)}
          aria-expanded={open}
          aria-controls={detailsId}
          className="rounded-sm border border-site-green-dark px-4 py-2.5 text-sm font-semibold text-site-green-dark hover:bg-site-surface-soft"
        >
          {open ? "Részletek bezárása" : "Részletek"}
        </button>
        {open && !editing && (
          <button
            type="button"
            onClick={() => setEditing(true)}
            className="rounded-sm bg-site-green px-4 py-2.5 text-sm font-semibold text-white hover:bg-site-green-hover"
          >
            Állapot módosítása
          </button>
        )}
      </div>
    </article>
  );
}

export default function AdminOrdersPage() {
  const [searchParams] = useSearchParams();
  const [orders, setOrders] = useState<Order[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [search, setSearch] = useState(() => searchParams.get("search") ?? "");
  const [status, setStatus] = useState<"all" | OrderStatus>("all");
  const [fulfillment, setFulfillment] = useState<"all" | FulfillmentMethod>("all");
  const [page, setPage] = useState(1);

  const load = useCallback(async (signal?: AbortSignal) => {
    setLoading(true);
    setError(null);

    try {
      setOrders(await getAdminOrders(signal));
    } catch (loadError) {
      if (!isAbortError(loadError)) {
        setError(getErrorMessage(loadError, "A rendelések betöltése nem sikerült."));
      }
    } finally {
      if (!signal?.aborted) setLoading(false);
    }
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal);
    return () => controller.abort();
  }, [load]);

  useEffect(() => {
    if (!message) return;
    const timer = window.setTimeout(() => setMessage(null), 3500);
    return () => clearTimeout(timer);
  }, [message]);

  const filtered = useMemo(() => {
    const query = search.trim().toLocaleLowerCase("hu-HU");

    return orders.filter((order) => {
      if (
        query &&
        ![order.orderNumber, order.customerName, order.customerEmail].some((value) =>
          value.toLocaleLowerCase("hu-HU").includes(query),
        )
      ) {
        return false;
      }
      if (status !== "all" && order.status !== status) return false;
      if (fulfillment !== "all" && order.fulfillmentMethod !== fulfillment) return false;
      return true;
    });
  }, [orders, search, status, fulfillment]);

  const pages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const current = Math.min(page, pages);
  const shown = filtered.slice((current - 1) * PAGE_SIZE, current * PAGE_SIZE);

  function updated(order: Order) {
    setOrders((items) => items.map((item) => (item.id === order.id ? order : item)));
    setMessage(`${order.orderNumber} állapota frissült.`);
  }

  return (
    <div className="space-y-6">
      <AdminPageHeader
        title="Rendelések"
        description="A beérkezett rendelések részletei, fizetése és teljesítési állapota."
      />

      <AdminFeedbackMessages message={message} error={error} />

      <section className="grid gap-4 rounded-sm border border-site-border bg-white p-4 shadow-sm md:grid-cols-3">
        <label className="text-sm font-semibold">
          Keresés
          <input
            value={search}
            onChange={(event) => {
              setSearch(event.target.value);
              setPage(1);
            }}
            placeholder="Rendelésszám, név vagy email"
            className="mt-1.5 w-full rounded-sm border border-site-border px-3 py-2.5"
          />
        </label>

        <label className="text-sm font-semibold">
          Állapot
          <select
            value={status}
            onChange={(event) => {
              setStatus(event.target.value === "all" ? "all" : Number(event.target.value) as OrderStatus);
              setPage(1);
            }}
            className="mt-1.5 w-full rounded-sm border border-site-border px-3 py-2.5"
          >
            <option value="all">Minden állapot</option>
            {statuses.map((value) => (
              <option key={value} value={value}>{getOrderStatusLabel(value)}</option>
            ))}
          </select>
        </label>

        <label className="text-sm font-semibold">
          Átvételi mód
          <select
            value={fulfillment}
            onChange={(event) => {
              setFulfillment(event.target.value === "all" ? "all" : Number(event.target.value) as FulfillmentMethod);
              setPage(1);
            }}
            className="mt-1.5 w-full rounded-sm border border-site-border px-3 py-2.5"
          >
            <option value="all">Minden mód</option>
            <option value="0">Személyes átvétel</option>
            <option value="1">Házhoz szállítás</option>
            <option value="2">Csomagautomata</option>
            <option value="3">GLS CsomagPont</option>
          </select>
        </label>
      </section>

      {loading ? (
        <AdminListSkeletonGrid gridClassName="grid gap-5" itemHeightClassName="h-64" />
      ) : shown.length === 0 ? (
        <AdminListEmptyState />
      ) : (
        <section className="grid min-w-0 items-start gap-5">
          {shown.map((order) => (
            <OrderCard key={order.id} order={order} onUpdated={updated} />
          ))}
        </section>
      )}

      {!loading && filtered.length > 0 && (
        <AdminPagination page={current} totalPages={pages} onPageChange={setPage} />
      )}
    </div>
  );
}
