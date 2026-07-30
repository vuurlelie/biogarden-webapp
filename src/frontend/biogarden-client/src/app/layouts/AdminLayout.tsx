import { useState, type ReactNode } from "react";
import { Link, NavLink, Outlet, useNavigate } from "react-router";
import { clearAuthSession, getAuthSession } from "../../features/auth/utils/authStorage";
import { ROUTES } from "../../shared/constants/routes";
import ScrollToTop from "../router/ScrollToTop";

type NavigationItem = {
  label: string;
  description: string;
  to: string;
  icon: ReactNode;
  end?: boolean;
};

const iconClass = "h-5 w-5 shrink-0";

const navigationItems: NavigationItem[] = [
  {
    label: "Áttekintés",
    description: "Adminisztrációs kezdőlap",
    to: ROUTES.admin,
    end: true,
    icon: (
      <svg aria-hidden="true" className={iconClass} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
        <path d="M4 13h6V4H4v9Zm0 7h6v-4H4v4Zm10 0h6v-9h-6v9Zm0-16v4h6V4h-6Z" />
      </svg>
    ),
  },
  {
    label: "Rendelések",
    description: "Beérkezett rendelések",
    to: ROUTES.adminOrders,
    icon: (
      <svg aria-hidden="true" className={iconClass} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
        <path d="M7 3h10v4H7V3ZM5 5H3v16h18V5h-2M7 12h10M7 16h7" />
      </svg>
    ),
  },
  {
    label: "Fogyasztói kérések",
    description: "Elállás, panasz, kérés",
    to: ROUTES.adminWithdrawalRequests,
    icon: (
      <svg aria-hidden="true" className={iconClass} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
        <path d="M5 4h14v16H5V4Zm4 5h6m-6 4h6m-6 4h4M18 6l2 2 3-4" />
      </svg>
    ),
  },
  {
    label: "Raktárkészlet",
    description: "Készlet és elérhetőség",
    to: ROUTES.adminInventory,
    icon: (
      <svg aria-hidden="true" className={iconClass} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
        <path d="M3 8h18v13H3V8Zm2-5h14l2 5H3l2-5Zm4 9h6" />
      </svg>
    ),
  },
  {
    label: "Felhasználók",
    description: "Fiókok és jogosultságok",
    to: ROUTES.adminAccounts,
    icon: (
      <svg aria-hidden="true" className={iconClass} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
        <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2m7-10a4 4 0 1 0 0-8 4 4 0 0 0 0 8Zm8-1v6m3-3h-6" />
      </svg>
    ),
  },
  {
    label: "Termékek",
    description: "Kínálat és termékadatok",
    to: ROUTES.adminProducts,
    icon: (
      <svg aria-hidden="true" className={iconClass} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
        <path d="m4 7 8-4 8 4-8 4-8-4Zm0 0v10l8 4m0-10v10m8-14v10l-8 4" />
      </svg>
    ),
  },
  {
    label: "Bejegyzések",
    description: "Tudástár kezelése",
    to: ROUTES.adminPosts,
    icon: (
      <svg aria-hidden="true" className={iconClass} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
        <path d="M5 3h14v18H5V3Zm4 5h6M9 12h6M9 16h4" />
      </svg>
    ),
  },
];

function AdminNavigation({ onNavigate }: { onNavigate?: () => void }) {
  return (
    <nav aria-label="Adminisztrációs menü" className="space-y-1.5">
      {navigationItems.map((item) => (
        <NavLink
          key={item.to}
          to={item.to}
          end={item.end}
          onClick={onNavigate}
          className={({ isActive }) => [
            "group flex items-center gap-3 rounded-sm px-3 py-3 transition-colors",
            isActive
              ? "bg-site-green text-white shadow-sm"
              : "text-white/80 hover:bg-white/10 hover:text-white",
          ].join(" ")}
        >
          {item.icon}
          <span className="min-w-0">
            <span className="block text-sm font-semibold">{item.label}</span>
            <span className="block truncate text-xs opacity-75">{item.description}</span>
          </span>
        </NavLink>
      ))}
    </nav>
  );
}

export default function AdminLayout() {
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const navigate = useNavigate();
  const auth = getAuthSession();

  function logout() {
    clearAuthSession();
    navigate(ROUTES.login, { replace: true });
  }

  return (
    <div className="min-h-screen bg-site-surface-soft text-site-text lg:flex">
      <ScrollToTop />

      <aside className="hidden h-screen w-72 shrink-0 flex-col bg-site-nav px-5 py-6 text-white lg:sticky lg:top-0 lg:flex">
        <Link to={ROUTES.admin} className="mb-8 block border-b border-white/15 pb-6">
          <span className="text-xs font-semibold uppercase tracking-[0.2em] text-white/65">Bakony Kincse</span>
          <span className="mt-1 block text-xl font-bold">Adminisztráció</span>
        </Link>

        <div className="min-h-0 flex-1 overflow-y-auto pr-1">
          <AdminNavigation />
        </div>

        <div className="mt-6 border-t border-white/15 pt-5">
          <p className="truncate text-xs text-white/60">{auth?.email}</p>
          <div className="mt-3 grid grid-cols-2 gap-2">
            <Link
              to={ROUTES.home}
              target="_blank"
              rel="noreferrer"
              className="rounded-sm border border-white/25 px-3 py-2 text-center text-xs font-semibold text-white hover:bg-white/10"
            >
              Weboldal
            </Link>
            <button type="button" onClick={logout} className="rounded-sm bg-white px-3 py-2 text-xs font-semibold text-site-nav hover:bg-site-surface-soft">
              Kijelentkezés
            </button>
          </div>
        </div>
      </aside>

      <div className="min-w-0 flex-1">
        <header className="sticky top-0 z-50 border-b border-site-border bg-white/95 shadow-sm backdrop-blur lg:hidden">
          <div className="flex min-h-16 items-center justify-between gap-4 px-4 sm:px-6">
            <Link to={ROUTES.admin} onClick={() => setMobileMenuOpen(false)} className="min-w-0">
              <span className="block text-[0.65rem] font-semibold uppercase tracking-[0.18em] text-site-green-dark">Bakony Kincse</span>
              <span className="block truncate text-lg font-bold text-site-heading">Adminisztráció</span>
            </Link>
            <button
              type="button"
              aria-label={mobileMenuOpen ? "Admin menü bezárása" : "Admin menü megnyitása"}
              aria-expanded={mobileMenuOpen}
              aria-controls="admin-mobile-menu"
              onClick={() => setMobileMenuOpen((open) => !open)}
              className="inline-flex min-h-11 items-center gap-2 rounded-sm border border-site-border px-3 py-2 font-semibold text-site-nav hover:bg-site-surface-soft"
            >
              <svg aria-hidden="true" className="h-5 w-5" viewBox="0 0 20 20" fill="currentColor"><path d="M2 4h16v2H2V4Zm0 5h16v2H2V9Zm0 5h16v2H2v-2Z" /></svg>
              <span className="text-sm">Menü</span>
            </button>
          </div>

          {mobileMenuOpen && (
            <div id="admin-mobile-menu" className="max-h-[calc(100vh-4rem)] overflow-y-auto bg-site-nav px-4 py-4 text-white sm:px-6">
              <AdminNavigation onNavigate={() => setMobileMenuOpen(false)} />
              <div className="mt-4 grid grid-cols-2 gap-3 border-t border-white/15 pt-4">
                <Link
                  to={ROUTES.home}
                  target="_blank"
                  rel="noreferrer"
                  onClick={() => setMobileMenuOpen(false)}
                  className="rounded-sm border border-white/25 px-4 py-3 text-center text-sm font-semibold text-white"
                >
                  Weboldal
                </Link>
                <button type="button" onClick={logout} className="rounded-sm bg-white px-4 py-3 text-sm font-semibold text-site-nav">
                  Kijelentkezés
                </button>
              </div>
            </div>
          )}
        </header>

        <main className="mx-auto w-full max-w-[100rem] p-4 sm:p-6 lg:p-8 xl:p-10">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
