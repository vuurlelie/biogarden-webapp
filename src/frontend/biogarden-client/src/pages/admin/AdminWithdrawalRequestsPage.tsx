import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router";
import {
  getAdminOrderWithdrawalRequests,
  markAdminOrderWithdrawalRequestHandled,
  type AdminOrderWithdrawalRequest,
} from "../../features/admin/api/adminOrdersApi";
import { formatOrderPrice, getOrderStatusLabel } from "../../features/account/utils/orderLabels";
import AdminFeedbackMessages from "../../shared/components/admin/AdminFeedbackMessages";
import AdminListEmptyState from "../../shared/components/admin/AdminListEmptyState";
import AdminListSkeletonGrid from "../../shared/components/admin/AdminListSkeletonGrid";
import AdminPageHeader from "../../shared/components/admin/AdminPageHeader";
import { ROUTES } from "../../shared/constants/routes";
import { getErrorMessage, isAbortError } from "../../shared/utils/errorHandling";

const DATE = new Intl.DateTimeFormat("hu-HU", {
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

export default function AdminWithdrawalRequestsPage() {
  const [requests, setRequests] = useState<AdminOrderWithdrawalRequest[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [status, setStatus] = useState<RequestStatusFilter>("pending");
  const [search, setSearch] = useState("");
  const [handlingId, setHandlingId] = useState<number | null>(null);

  const load = useCallback(async (signal?: AbortSignal) => {
    setLoading(true);
    setError(null);

    try {
      setRequests(await getAdminOrderWithdrawalRequests(signal));
    } catch (loadError) {
      if (!isAbortError(loadError)) {
        setError(getErrorMessage(loadError, "Az elállási vagy fogyasztói kérések betöltése nem sikerült."));
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

    return requests.filter((request) => {
      if (status === "pending" && request.status !== 1) return false;
      if (status === "handled" && request.status !== 2) return false;
      if (
        query &&
        ![request.orderNumber, request.customerName, request.customerEmail, request.reason].some((value) =>
          value.toLocaleLowerCase("hu-HU").includes(query),
        )
      ) {
        return false;
      }
      return true;
    });
  }, [requests, search, status]);

  async function markHandled(request: AdminOrderWithdrawalRequest) {
    setHandlingId(request.id);
    setError(null);

    try {
      const updated = await markAdminOrderWithdrawalRequestHandled(request.id);
      setRequests((items) => items.map((item) => (item.id === updated.id ? updated : item)));
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
        description="A rendeléshez kapcsolódó elállási, panasz- és fogyasztói kérések feldolgozási listája."
      />

      <AdminFeedbackMessages message={message} error={error} />

      <section className="grid gap-4 rounded-sm border border-site-border bg-white p-4 shadow-sm md:grid-cols-2">
        <label className="text-sm font-semibold">
          Keresés
          <input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Rendelésszám, név, email vagy kérés szövege"
            className="mt-1.5 w-full rounded-sm border border-site-border px-3 py-2.5"
          />
        </label>

        <label className="text-sm font-semibold">
          Állapot
          <select
            value={status}
            onChange={(event) => setStatus(event.target.value as RequestStatusFilter)}
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
          {filtered.map((request) => (
            <article key={request.id} className="rounded-sm border border-site-border bg-white p-5 shadow-sm">
              <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <h2 className="text-xl font-bold text-site-heading">{request.orderNumber}</h2>
                    <span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${request.status === 1 ? "bg-site-danger-soft text-site-danger" : "bg-site-surface-soft text-site-green-dark"}`}>
                      {requestStatusLabels[request.status]}
                    </span>
                  </div>
                  <p className="mt-2 text-sm text-site-muted">
                    {request.customerName} · {request.customerEmail}
                  </p>
                </div>

                <div className="flex flex-wrap gap-3">
                  <Link
                    to={orderSearchUrl(request.orderNumber)}
                    className="rounded-sm border border-site-green-dark px-4 py-2.5 text-sm font-semibold text-site-green-dark hover:bg-site-surface-soft"
                  >
                    Kapcsolódó rendelés
                  </Link>
                  {request.status === 1 && (
                    <button
                      type="button"
                      onClick={() => void markHandled(request)}
                      disabled={handlingId === request.id}
                      className="rounded-sm bg-site-green px-4 py-2.5 text-sm font-semibold text-white hover:bg-site-green-hover disabled:cursor-wait disabled:opacity-60"
                    >
                      {handlingId === request.id ? "Mentés..." : "Feldolgozva"}
                    </button>
                  )}
                </div>
              </div>

              <dl className="mt-5 grid gap-4 rounded-sm bg-site-surface-soft p-4 text-sm sm:grid-cols-3">
                <div>
                  <dt className="font-semibold text-site-heading">Rendelés állapota</dt>
                  <dd className="mt-1 text-site-text">{getOrderStatusLabel(request.orderStatus)}</dd>
                </div>
                <div>
                  <dt className="font-semibold text-site-heading">Rendelés összege</dt>
                  <dd className="mt-1 text-site-text">{formatOrderPrice(request.orderTotalPrice)}</dd>
                </div>
                <div>
                  <dt className="font-semibold text-site-heading">Beérkezett</dt>
                  <dd className="mt-1 text-site-text">{DATE.format(new Date(request.requestedAtUtc))}</dd>
                </div>
              </dl>

              <div className="mt-5 rounded-sm border border-site-border p-4 text-sm leading-6 text-site-text">
                {request.reason}
              </div>

              {request.handledAtUtc && (
                <p className="mt-3 text-xs text-site-muted">
                  Feldolgozva: {DATE.format(new Date(request.handledAtUtc))}
                </p>
              )}
            </article>
          ))}
        </section>
      )}
    </div>
  );
}
