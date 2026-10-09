import { SITE_TIME_ZONE, parseApiDate } from "../../shared/utils/dateTime";
import AdminPagination from "../../shared/components/admin/AdminPagination";
import { useCallback, useEffect, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router";
import {
  getAdminOrderWithdrawalRequests,
  markAdminOrderWithdrawalRequestHandled,
  type AdminOrderWithdrawalRequest,
  type AdminOrderRequestThread,
} from "../../features/admin/api/adminOrdersApi";
import { formatOrderPrice, getOrderStatusLabel } from "../../features/account/utils/orderLabels";
import AdminFeedbackMessages from "../../shared/components/admin/AdminFeedbackMessages";
import AdminListEmptyState from "../../shared/components/admin/AdminListEmptyState";
import AdminListSkeletonGrid from "../../shared/components/admin/AdminListSkeletonGrid";
import AdminPageHeader from "../../shared/components/admin/AdminPageHeader";
import { ROUTES } from "../../shared/constants/routes";
import { getErrorMessage, isAbortError } from "../../shared/utils/errorHandling";

const DATE = new Intl.DateTimeFormat("hu-HU", { timeZone: SITE_TIME_ZONE,
  year: "numeric",
  month: "short",
  day: "numeric",
  hour: "2-digit",
  minute: "2-digit",
});

type RequestStatusFilter = "all" | "pending" | "handled";

const requestStatusLabels: Record<AdminOrderWithdrawalRequest["status"], string> = {
  1: "Feldolgozásra vár",
  2: "Feldolgozva",
};

function orderSearchUrl(orderNumber: string) {
  return `${ROUTES.adminOrders}?search=${encodeURIComponent(orderNumber)}`;
}

const PAGE_SIZE = 12;

export default function AdminWithdrawalRequestsPage() {
  const [requests, setRequests] = useState<AdminOrderRequestThread[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [params, setParams] = useSearchParams();
  const status: RequestStatusFilter = params.get("status") === "all" ? "all"
    : params.get("status") === "handled" ? "handled" : "pending";
  const search = params.get("search") ?? "";
  function setFilter(key: string, value: string) {
    setParams(current => {
      const next = new URLSearchParams(current);
      if (value) next.set(key, value); else next.delete(key);
      return next;
    }, { replace: true });
    setPage(1);
  }
  const [page, setPage] = useState(1);
  const [responses, setResponses] = useState<Record<number, string>>({});
  const [handlingId, setHandlingId] = useState<number | null>(null);

  const [pages, setTotalPages] = useState(1);
  const requestId = useRef(0);
  const mounted = useRef(true);

  const load = useCallback(async (signal?: AbortSignal, background = false) => {
    if (!mounted.current) return;
    const id = ++requestId.current;
    if (!background) setLoading(true);
    setError(null);

    try {
      const result = await getAdminOrderWithdrawalRequests({ page, pageSize: PAGE_SIZE, search, status: status === "all" ? undefined : status === "pending" ? 1 : 2 }, signal);
      if (!mounted.current || signal?.aborted || id !== requestId.current) return;
      setRequests(result.items);
      setTotalPages(result.totalPages);
      if (result.page !== page) setPage(result.page);
    } catch (loadError) {
      if (mounted.current && !signal?.aborted && id === requestId.current && !isAbortError(loadError)) {
        setError(getErrorMessage(loadError, "Az elállási vagy fogyasztói kérések betöltése nem sikerült."));
      }
    } finally {
      if (mounted.current && !signal?.aborted && id === requestId.current) setLoading(false);
    }
  }, [page, search, status]);

  const latestLoad = useRef(load);
  useEffect(() => {
    latestLoad.current = load;
    mounted.current = true;
    ++requestId.current;
    const controller = new AbortController();
    const timer = window.setTimeout(() => void load(controller.signal), search.trim() ? 250 : 0);
    return () => { mounted.current = false; window.clearTimeout(timer); controller.abort(); };
  }, [load, search]);

  useEffect(() => {
    if (!message) return;
    const timer = window.setTimeout(() => setMessage(null), 3500);
    return () => clearTimeout(timer);
  }, [message]);

  const current = page;
  const filtered = requests;

  async function markHandled(request: AdminOrderWithdrawalRequest) {
    setHandlingId(request.id);
    setError(null);

    try {
      const updated = await markAdminOrderWithdrawalRequestHandled(request.id, responses[request.id]);
      setRequests((items) => items.map(thread => ({ ...thread, messages: thread.messages.map(item => item.id === updated.id ? updated : item) })));
      void latestLoad.current(undefined, true);
      setMessage(`${request.orderNumber} kérését feldolgozottra állítottad.`);
    } catch (handleError) {
      setError(getErrorMessage(handleError, "A kérés feldolgozottra állítása nem sikerült."));
    } finally {
      setHandlingId(null);
    }
  }

  return (
    <div className="space-y-6">
      <AdminPageHeader
        title="Elállási és fogyasztói kérések"
        description="A rendeléshez kapcsolódó elállási, panasz- és fogyasztói kérések rendelésenként csoportosítva. A szűrőknek megfelelő rendelés minden üzenete látható, a feldolgozott előzményekkel együtt."
      />

      <AdminFeedbackMessages message={message} error={error} />

      <section className="grid gap-4 rounded-sm border border-site-border bg-white p-4 shadow-sm md:grid-cols-2">
        <label className="text-sm font-semibold">
          Keresés
          <input
            maxLength={200}
            value={search}
            onChange={(event) => { setFilter("search", event.target.value); }}
            placeholder="Rendelésszám, név, email vagy kérés szövege"
            className="mt-1.5 w-full rounded-sm border border-site-border px-3 py-2.5"
          />
        </label>

        <label className="text-sm font-semibold">
          Állapot
          <select
            value={status}
            onChange={(event) => { setFilter("status", event.target.value); }}
            className="mt-1.5 w-full rounded-sm border border-site-border px-3 py-2.5"
          >
            <option value="pending">Feldolgozásra vár</option>
            <option value="handled">Feldolgozva</option>
            <option value="all">Minden kérés</option>
          </select>
        </label>
      </section>

      {loading ? (
        <AdminListSkeletonGrid gridClassName="grid gap-5" itemHeightClassName="h-60" />
      ) : filtered.length === 0 ? (
        <AdminListEmptyState
          title="Nincs ilyen kérés"
          description="A kiválasztott szűrőkkel jelenleg nincs feldolgozandó elállási vagy fogyasztói kérés."
        />
      ) : (
        <section className="grid gap-5">
          {filtered.map(thread => {
            const order = thread.messages[0];
            return <article key={thread.orderId} className="rounded-sm border border-site-border bg-white p-5 shadow-sm">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <h2 className="text-xl font-bold text-site-heading">{order.orderNumber}</h2>
                  <p className="mt-2 text-sm text-site-muted">{order.customerName} · {order.customerEmail}</p>
                  <p className="mt-2 text-sm">{getOrderStatusLabel(order.orderStatus)} · {formatOrderPrice(order.orderTotalPrice)}</p>
                </div>
                <Link to={orderSearchUrl(order.orderNumber)} className="rounded-sm border border-site-green-dark px-4 py-2.5 text-sm font-semibold text-site-green-dark hover:bg-site-surface-soft">Kapcsolódó rendelés</Link>
              </div>
              <ol className="mt-5 space-y-4" aria-label="Rendeléshez kapcsolódó üzenetek">
                {thread.messages.map(request => <li key={request.id} className="rounded-sm border border-site-border p-4">
                  <div className="flex flex-wrap items-center justify-between gap-3 text-sm">
                    <div>
                      <time dateTime={request.requestedAtUtc}>{DATE.format(parseApiDate(request.requestedAtUtc))}</time>
                      <span className="ml-3 font-semibold">{requestStatusLabels[request.status]}</span>
                    </div>
                    {request.status === 1 && <button type="button" onClick={() => void markHandled(request)}
                      disabled={handlingId !== null} className="rounded-sm bg-site-green px-4 py-2 text-white disabled:opacity-60">
                      {handlingId === request.id ? "Mentés..." : "Feldolgozva"}
                    </button>}
                  </div>
                  <p className="mt-3 whitespace-pre-wrap text-sm leading-6 text-site-text">{request.reason}</p>
                  {request.status === 1 && <div className="mt-3">
                    <label htmlFor={`response-${request.id}`} className="text-sm font-semibold">Válasz a vásárlónak (nem kötelező)</label>
                    <p id={`response-hint-${request.id}`} className="text-xs text-site-muted">A vásárló ezt a választ látni fogja a rendelése részleteinél.</p>
                    <textarea id={`response-${request.id}`} rows={3} maxLength={1000}
                      disabled={handlingId !== null} value={responses[request.id] ?? ""}
                      aria-describedby={`response-hint-${request.id} response-count-${request.id}`}
                      onChange={event => setResponses(current => ({ ...current, [request.id]: event.target.value }))}
                      className="mt-2 block w-full rounded-sm border border-site-border p-3 text-sm" />
                    <p id={`response-count-${request.id}`} className="mt-1 text-right text-xs text-site-muted">{(responses[request.id] ?? "").length} / 1000</p>
                  </div>}
                  {request.adminResponse && <div className="mt-3 rounded-sm bg-site-surface-soft p-3 text-sm">
                    <strong>Válasz a vásárlónak</strong><p className="mt-1 whitespace-pre-wrap">{request.adminResponse}</p>
                  </div>}
                  {request.handledAtUtc && <p className="mt-2 text-xs text-site-muted">Feldolgozva: {DATE.format(parseApiDate(request.handledAtUtc))}</p>}
                </li>)}
              </ol>
            </article>;
          })}
        </section>
      )}
      {!loading && requests.length > 0 && <AdminPagination page={current} totalPages={pages} onPageChange={setPage} />}
    </div>
  );
}
