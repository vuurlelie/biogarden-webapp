import SkipLink from "../../shared/components/layout/SkipLink";
import { NavLink, Outlet, useNavigate } from "react-router";
import {
  logout as endBrowserSession,
  getAuthSession,
} from "../../features/auth/utils/authStorage";
import Footer from "../../shared/components/layout/Footer";
import Navbar from "../../shared/components/layout/Navbar";
import BackToTopBtn from "../../shared/components/ui/BackToTopBtn";
import { ROUTES } from "../../shared/constants/routes";
import ScrollToTop from "../router/ScrollToTop";
import { useCart } from "../../features/cart/context/useCart";
import RouteAnalytics from "../../features/analytics/components/RouteAnalytics";
import ConsentBanner from "../../features/analytics/components/ConsentBanner";

const accountLinkClass = ({ isActive }: { isActive: boolean }) =>
  [
    "min-w-0 rounded-sm px-4 py-3 text-left text-sm font-semibold transition-colors lg:whitespace-nowrap",
    isActive
      ? "bg-site-green-dark text-white"
      : "text-site-nav hover:bg-site-surface-soft hover:text-site-green-dark",
  ].join(" ");

const accountDeleteLinkClass = ({ isActive }: { isActive: boolean }) =>
  [
    "min-w-0 rounded-sm px-4 py-3 text-left text-sm font-semibold transition-colors lg:whitespace-nowrap",
    isActive
      ? "bg-site-danger text-white"
      : "text-site-danger hover:bg-site-danger-soft",
  ].join(" ");

export default function AccountLayout() {
  const navigate = useNavigate();
  const auth = getAuthSession();
  const { cart } = useCart();

  async function handleLogout() {
    try {
      await endBrowserSession();
    } catch (error) {
      window.alert(error instanceof Error ? error.message : "A kijelentkezés nem sikerült.");
      return;
    }
    navigate(ROUTES.home, { replace: true });
  }

  return (
    <div className="flex min-h-screen flex-col bg-white text-site-text">
      <SkipLink />
      <ScrollToTop />
      <RouteAnalytics />
      <Navbar
        isAuthenticated
        isAdmin={auth?.roles.includes("Admin") ?? false}
        cartItemCount={cart?.totalItemCount ?? 0}
      />

      <main id="main-content" tabIndex={-1} aria-label="Fő tartalom" className="relative z-20 w-full flex-1 shadow-[0_12px_30px_rgba(48,55,43,0.14)]">
        <div className="mx-auto grid w-full max-w-7xl gap-6 px-4 py-6 sm:px-6 lg:grid-cols-[240px_minmax(0,1fr)] lg:px-8 lg:py-10">
        <aside className="min-w-0 self-start rounded-sm border border-site-border bg-white p-3 shadow-md lg:sticky lg:top-20">
          <div className="border-b border-site-border px-3 pb-4 pt-2">
            <p className="text-xs font-bold uppercase tracking-[0.15em] text-site-green-dark">
              Fiókom
            </p>
            <p className="mt-1 truncate text-sm text-site-muted" title={auth?.email}>
              {auth?.email}
            </p>
          </div>

          <nav className="mt-3 flex min-w-0 flex-col gap-2" aria-label="Fiók navigáció">
            <NavLink to={ROUTES.account} end className={accountLinkClass}>
              Áttekintés
            </NavLink>
            <NavLink to={ROUTES.accountProfile} className={accountLinkClass}>
              Személyes adatok
            </NavLink>
            <NavLink to={ROUTES.accountAddresses} className={accountLinkClass}>
              Mentett címek
            </NavLink>
            <NavLink to={ROUTES.accountOrders} className={accountLinkClass}>
              Rendeléseim
            </NavLink>
            <NavLink to={ROUTES.accountDelete} className={accountDeleteLinkClass}>
              Fiók törlése
            </NavLink>
          </nav>

          <button
            type="button"
            onClick={handleLogout}
            className="mt-3 w-full rounded-sm border border-site-green-dark/45 px-4 py-2.5 text-left text-sm font-semibold text-site-green-dark transition hover:bg-site-surface-soft"
          >
            Kijelentkezés
          </button>
        </aside>

        <section className="min-w-0">
          <Outlet />
        </section>
        </div>
      </main>

      <BackToTopBtn />
      <Footer />
      <ConsentBanner />
    </div>
  );
}
