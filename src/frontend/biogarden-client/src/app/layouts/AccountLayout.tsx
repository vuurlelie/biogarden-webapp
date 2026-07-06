import { NavLink, Outlet, useNavigate } from "react-router-dom";
import {
  clearAuthSession,
  getAuthSession,
} from "../../features/auth/utils/authStorage";
import Footer from "../../shared/components/layout/Footer";
import Navbar from "../../shared/components/layout/Navbar";
import BackToTopBtn from "../../shared/components/ui/BackToTopBtn";
import { ROUTES } from "../../shared/constants/routes";
import ScrollToTop from "../router/ScrollToTop";

const accountLinkClass = ({ isActive }: { isActive: boolean }) =>
  [
    "whitespace-nowrap rounded-sm px-4 py-3 text-sm font-semibold transition-colors",
    isActive
      ? "bg-site-green-dark text-white"
      : "text-site-nav hover:bg-site-surface-soft hover:text-site-green-dark",
  ].join(" ");

export default function AccountLayout() {
  const navigate = useNavigate();
  const auth = getAuthSession();

  function handleLogout() {
    clearAuthSession();
    navigate(ROUTES.home, { replace: true });
  }

  return (
    <div className="min-h-screen bg-white text-site-text">
      <ScrollToTop />
      <Navbar
        isAuthenticated
        isAdmin={auth?.roles.includes("Admin") ?? false}
        cartItemCount={0}
      />

      <main className="mx-auto grid w-full max-w-7xl gap-6 px-4 py-6 sm:px-6 lg:grid-cols-[240px_minmax(0,1fr)] lg:px-8 lg:py-10">
        <aside className="self-start rounded-sm border border-site-border bg-white p-3 shadow-md lg:sticky lg:top-20">
          <div className="border-b border-site-border px-3 pb-4 pt-2">
            <p className="text-xs font-bold uppercase tracking-[0.15em] text-site-green-dark">
              Fiókom
            </p>
            <p className="mt-1 truncate text-sm text-site-muted" title={auth?.email}>
              {auth?.email}
            </p>
          </div>

          <nav className="mt-3 flex gap-2 overflow-x-auto lg:flex-col" aria-label="Fiók navigáció">
            <NavLink to={ROUTES.account} end className={accountLinkClass}>
              Áttekintés
            </NavLink>
            <NavLink to={ROUTES.accountProfile} className={accountLinkClass}>
              Személyes adatok
            </NavLink>
          </nav>

          <button
            type="button"
            onClick={handleLogout}
            className="mt-3 w-full rounded-sm border border-site-border px-4 py-2.5 text-left text-sm font-semibold text-site-danger transition hover:bg-site-danger-soft"
          >
            Kijelentkezés
          </button>
        </aside>

        <section className="min-w-0">
          <Outlet />
        </section>
      </main>

      <BackToTopBtn />
      <Footer />
    </div>
  );
}

