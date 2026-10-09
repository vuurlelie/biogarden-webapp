import { SITE_TIME_ZONE, parseApiDate } from "../../shared/utils/dateTime";
import { useCallback, useEffect, useState, type ReactNode } from "react";
import { Link } from "react-router";
import { getAdminDashboard } from "../../features/admin/api/dashboardApi";
import type { AdminDashboard } from "../../features/admin/types/dashboard.types";
import AdminPageHeader from "../../shared/components/admin/AdminPageHeader";
import { ROUTES } from "../../shared/constants/routes";
import { getErrorMessage, isAbortError } from "../../shared/utils/errorHandling";

const PRICE = new Intl.NumberFormat("hu-HU", { style: "currency", currency: "HUF", maximumFractionDigits: 0 });
const DATE = new Intl.DateTimeFormat("hu-HU", { timeZone: SITE_TIME_ZONE, year: "numeric", month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" });

const statusLabels: Record<number, string> = {
  0: "Függőben",
  1: "Visszaigazolva",
  2: "Kiszállítva",
  3: "Teljesítve",
  4: "Lemondva",
};

const fulfillmentLabels: Record<number, string> = {
  0: "Személyes átvétel",
  1: "Házhoz szállítás",
  2: "Csomagautomata",
  3: "GLS CsomagPont",
};

type SummaryCardProps = {
  label: string;
  value: string | number;
  detail: string;
  icon: ReactNode;
  to: string;
  warning?: boolean;
};

function SummaryCard({ label, value, detail, icon, to, warning = false }: SummaryCardProps) {
  return (
    <Link
      to={to}
      className={`group rounded-sm border bg-white p-5 shadow-sm transition hover:-translate-y-0.5 hover:shadow-md focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-site-green ${warning ? "border-site-danger/30" : "border-site-border"}`}
      aria-label={`${label}: ${value}. ${detail}`}
    >
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0">
          <p className="text-sm font-semibold text-site-muted">{label}</p>
          <p className={`mt-2 text-3xl font-bold ${warning ? "text-site-danger" : "text-site-heading"}`}>{value}</p>
          <p className="mt-2 text-xs leading-5 text-site-muted">{detail}</p>
        </div>
        <span className={`grid h-11 w-11 shrink-0 place-items-center rounded-full ${warning ? "bg-site-danger-soft text-site-danger" : "bg-site-surface-soft text-site-green-dark"}`}>
          {icon}
        </span>
      </div>
      <span className="mt-4 inline-flex items-center gap-1 text-xs font-semibold text-site-green-dark group-hover:underline">
        Részletek <span aria-hidden="true">→</span>
      </span>
    </Link>
  );
}

const iconClass = "h-5 w-5";
const icons = {
  product: <svg aria-hidden="true" className={iconClass} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"><path d="m4 7 8-4 8 4-8 4-8-4Zm0 0v10l8 4m0-10v10m8-14v10l-8 4" /></svg>,
  order: <svg aria-hidden="true" className={iconClass} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"><path d="M6 3h12v18H6V3Zm4 5h4m-5 4h6m-6 4h6" /></svg>,
  stock: <svg aria-hidden="true" className={iconClass} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"><path d="M3 8h18v13H3V8Zm2-5h14l2 5H3l2-5Zm4 9h6" /></svg>,
  user: <svg aria-hidden="true" className={iconClass} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"><path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2m7-10a4 4 0 1 0 0-8 4 4 0 0 0 0 8Zm7 10v-2a4 4 0 0 0-3-3.87" /></svg>,
  post: <svg aria-hidden="true" className={iconClass} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"><path d="M5 3h14v18H5V3Zm4 5h6m-6 4h6m-6 4h4" /></svg>,
  value: <svg aria-hidden="true" className={iconClass} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"><path d="M12 2v20m5-16H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6" /></svg>,
  request: <svg aria-hidden="true" className={iconClass} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"><path d="M5 4h14v16H5V4Zm4 5h6m-6 4h6m-6 4h4" /></svg>,
};

function LoadingCards() {
  return <div aria-label="Betöltés" className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">{Array.from({ length: 6 }, (_, index) => <div key={index} className="h-36 animate-pulse rounded-sm border border-site-border bg-white" />)}</div>;
}

export default function AdminDashboardPage() {
  const [dashboard, setDashboard] = useState<AdminDashboard | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const loadDashboard = useCallback(async (signal?: AbortSignal) => {
    setLoading(true);
    setError(null);
    try {
      setDashboard(await getAdminDashboard(signal));
    } catch (loadError) {
      if (isAbortError(loadError)) return;
      setError(getErrorMessage(loadError, "Az adatok betöltése nem sikerült."));
    } finally {
      if (!signal?.aborted) setLoading(false);
    }
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    void loadDashboard(controller.signal);
    return () => controller.abort();
  }, [loadDashboard]);

  return (
    <div className="space-y-6">
      <section className="grid gap-4 lg:grid-cols-3" aria-label="Adminisztrációs összefoglaló">
        <div className="lg:col-span-2">
          <AdminPageHeader
            eyebrow="Bakony Kincse"
            title="Adminisztrációs áttekintés"
          />
        </div>
        {loading ? (
          <div aria-label="Aktív rendelések értéke betöltése" className="h-36 animate-pulse rounded-sm border border-site-border bg-white" />
        ) : dashboard && (
          <SummaryCard to={ROUTES.adminOrders} label="Aktív rendelések értéke" value={PRICE.format(dashboard.activeOrderValue)} detail="A lemondott rendelések nélkül" icon={icons.value} />
        )}
      </section>

      {error && (
        <div role="alert" className="flex flex-col gap-3 rounded-sm border border-site-danger/25 bg-site-danger-soft p-4 text-site-danger sm:flex-row sm:items-center sm:justify-between">
          <span>{error}</span>
          <button type="button" onClick={() => void loadDashboard()} className="self-start rounded-sm border border-site-danger px-4 py-2 text-sm font-semibold sm:self-auto">Újrapróbálás</button>
        </div>
      )}

      {loading ? <LoadingCards /> : dashboard && (
        <>
          <section aria-labelledby="summary-heading">
            <h2 id="summary-heading" className="sr-only">Összesített adatok</h2>
            <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
              <SummaryCard to={ROUTES.adminOrders} label="Függőben lévő rendelések" value={dashboard.pendingOrderCount} detail={`${dashboard.todayOrderCount} rendelés érkezett ma`} icon={icons.order} />
              <SummaryCard to={ROUTES.adminWithdrawalRequests} label="Fogyasztói kérések" value={dashboard.pendingWithdrawalRequestCount} detail="Feldolgozásra váró elállási vagy fogyasztói kérés" icon={icons.request} warning={dashboard.pendingWithdrawalRequestCount > 0} />
              <SummaryCard to={ROUTES.adminInventory} label="Alacsony készlet" value={dashboard.lowStockProductCount} detail="Legfeljebb 5 darab van raktáron" icon={icons.stock} warning={dashboard.lowStockProductCount > 0} />
              <SummaryCard to={ROUTES.adminProducts} label="Termékek" value={dashboard.productCount} detail={`${dashboard.availableProductCount} jelenleg elérhető`} icon={icons.product} />
              <SummaryCard to={ROUTES.adminPosts} label="Közzétett bejegyzések" value={dashboard.publishedPostCount} detail="A tudástárban látható tartalom" icon={icons.post} />
              <SummaryCard to={ROUTES.adminAccounts} label="Felhasználók" value={dashboard.userCount} detail="Regisztrált felhasználói fiók" icon={icons.user} />
            </div>
          </section>

          <div className="grid items-start gap-6 xl:grid-cols-[minmax(0,1.35fr)_minmax(20rem,0.65fr)]">
            <section className="rounded-sm border border-site-border bg-white p-5 shadow-sm sm:p-6" aria-labelledby="recent-orders-heading">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <h2 id="recent-orders-heading" className="text-xl font-bold text-site-heading">Legutóbbi rendelések</h2>
                <Link to={ROUTES.adminOrders} className="text-sm font-semibold text-site-green-dark underline-offset-4 hover:underline">Összes rendelés</Link>
              </div>
              {dashboard.recentOrders.length === 0 ? <p className="mt-5 rounded-sm bg-site-surface-soft p-4 text-sm text-site-muted">Még nem érkezett rendelés.</p> : (
                <div className="mt-5 grid gap-4 md:grid-cols-2">
                  {dashboard.recentOrders.map((order) => (
                    <article key={order.id} className="rounded-sm border border-site-border p-4">
                      <div className="flex items-start justify-between gap-3">
                        <div className="min-w-0">
                          <p className="truncate font-bold text-site-heading">{order.orderNumber}</p>
                          <p className="mt-1 truncate text-sm text-site-muted">{order.customerName}</p>
                        </div>
                        <span className={`shrink-0 rounded-full px-2.5 py-1 text-xs font-semibold ${order.status === 4 ? "bg-site-danger-soft text-site-danger" : "bg-site-surface-soft text-site-green-dark"}`}>{statusLabels[order.status] ?? "Ismeretlen"}</span>
                      </div>
                      <dl className="mt-4 grid grid-cols-2 gap-x-4 gap-y-3 text-sm">
                        <div><dt className="text-xs text-site-muted">Átvétel</dt><dd className="mt-1 font-semibold text-site-text">{fulfillmentLabels[order.fulfillmentMethod] ?? "Ismeretlen"}</dd></div>
                        <div><dt className="text-xs text-site-muted">Termékek</dt><dd className="mt-1 font-semibold text-site-text">{order.itemCount} db</dd></div>
                        <div><dt className="text-xs text-site-muted">Érkezett</dt><dd className="mt-1 text-site-text">{DATE.format(parseApiDate(order.createdAtUtc))}</dd></div>
                        <div><dt className="text-xs text-site-muted">Összeg</dt><dd className="mt-1 font-bold text-site-heading">{PRICE.format(order.totalPrice)}</dd></div>
                      </dl>
                    </article>
                  ))}
                </div>
              )}
            </section>

            <div className="space-y-6">
              <section className="rounded-sm border border-site-border bg-white p-5 shadow-sm sm:p-6" aria-labelledby="withdrawal-requests-heading">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <h2 id="withdrawal-requests-heading" className="text-xl font-bold text-site-heading">Fogyasztói kérések</h2>
                  <Link to={ROUTES.adminWithdrawalRequests} className="text-sm font-semibold text-site-green-dark underline-offset-4 hover:underline">Kérések kezelése</Link>
                </div>
                {dashboard.pendingWithdrawalRequests.length === 0 ? <p className="mt-5 rounded-sm bg-site-surface-soft p-4 text-sm text-site-muted">Nincs feldolgozásra váró kérés.</p> : (
                  <div className="mt-5 space-y-3">
                    {dashboard.pendingWithdrawalRequests.map((request) => (
                      <article key={request.id} className="rounded-sm border border-site-danger/25 p-4">
                        <p className="font-semibold text-site-heading">{request.orderNumber}</p>
                        <p className="mt-1 text-sm text-site-muted">{request.customerName}</p>
                        <p className="mt-2 text-xs text-site-muted">{DATE.format(parseApiDate(request.requestedAtUtc))}</p>
                      </article>
                    ))}
                  </div>
                )}
              </section>

              <section className="rounded-sm border border-site-border bg-white p-5 shadow-sm sm:p-6" aria-labelledby="low-stock-heading">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <h2 id="low-stock-heading" className="text-xl font-bold text-site-heading">Készletfigyelő</h2>
                  <Link to={ROUTES.adminInventory} className="text-sm font-semibold text-site-green-dark underline-offset-4 hover:underline">Raktárkészlet</Link>
                </div>
                {dashboard.lowStockProducts.length === 0 ? <p className="mt-5 rounded-sm bg-site-surface-soft p-4 text-sm text-site-muted">Nincs alacsony készletű termék.</p> : (
                  <div className="mt-5 space-y-3">
                    {dashboard.lowStockProducts.map((product) => (
                      <article key={product.id} className="flex items-center justify-between gap-4 rounded-sm border border-site-border p-4">
                        <div className="min-w-0"><p className="truncate font-semibold text-site-heading">{product.name}</p><p className="mt-1 text-xs text-site-muted">{(product.available && product.stockQuantity > 0) ? "Elérhető" : "Nem elérhető"}</p></div>
                        <span className={`shrink-0 text-lg font-bold ${product.stockQuantity === 0 ? "text-site-danger" : "text-site-heading"}`}>{product.stockQuantity} db</span>
                      </article>
                    ))}
                  </div>
                )}
              </section>
            </div>
          </div>
        </>
      )}
    </div>
  );
}
