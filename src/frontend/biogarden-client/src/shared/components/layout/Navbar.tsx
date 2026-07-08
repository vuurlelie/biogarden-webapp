import { useEffect, useMemo, useState } from "react";
import { Link, NavLink, useNavigate } from "react-router-dom";
import { clearAuthSession } from "../../../features/auth/utils/authStorage";
import logo from "../../../assets/images/logo.png";
import bakonykincse from "../../../assets/images/bakonykincse.png";
import { ROUTES } from "../../constants/routes";

type NavbarProps = {
  isAuthenticated?: boolean;
  isAdmin?: boolean;
  cartItemCount?: number;
};

type ProductMenuItem = {
  label: string;
  to: string;
};

const PRODUCT_MENU_ITEMS: ProductMenuItem[] = [
  { label: "Zöldségek", to: `${ROUTES.products}?category=zoldsegek` },
  { label: "Fűszerek, gyógyteák", to: `${ROUTES.products}?category=fuszerek-gyogyteak` },
  { label: "Illóolajok, hidrolátumok", to: `${ROUTES.products}?category=illoolajok-hidrolatumok` },
];

export default function Navbar({
  isAuthenticated = false,
  isAdmin = false,
  cartItemCount = 0,
}: NavbarProps) {
  const navigate = useNavigate();
  const [isScrolled, setIsScrolled] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [mobileAccountOpen, setMobileAccountOpen] = useState(false);
  const [progress, setProgress] = useState(0);

  useEffect(() => {
    const onScroll = () => {
      const doc = document.documentElement;
      const scrolled = doc.scrollTop;
      const max = doc.scrollHeight - window.innerHeight;

      setProgress(max > 0 ? Math.min(100, Math.round((scrolled / max) * 100)) : 0);
      setIsScrolled(scrolled > 0);
    };

    window.addEventListener("scroll", onScroll, { passive: true });
    onScroll();

    return () => window.removeEventListener("scroll", onScroll);
  }, []);

  const closeMobile = () => {
    setMobileOpen(false);
    setMobileAccountOpen(false);
  };

  const logout = () => {
    clearAuthSession();
    closeMobile();
    navigate(ROUTES.home, { replace: true });
  };

  const goContact = () => {
    closeMobile();
    document.getElementById("contact")?.scrollIntoView({
      behavior: "smooth",
      block: "start",
    });
  };

  const goTop = () => {
    window.scrollTo({ top: 0, behavior: "smooth" });
    closeMobile();
  };

  const navLinkClass = useMemo(
  () =>
    ({ isActive }: { isActive: boolean }) =>
      [
        "relative inline-flex items-center text-[0.8rem] lg:text-base uppercase tracking-wide transition-colors duration-200",
        "after:content-[''] after:absolute after:left-0 after:-bottom-1 after:h-[2px] after:w-full after:bg-site-green",
        "after:origin-left after:scale-x-0 after:transition-transform after:duration-300",
        isActive
          ? "font-bold text-site-green-dark after:scale-x-100"
          : "text-site-nav hover:text-site-green-dark hover:font-semibold",
      ].join(" "),
  [],
);

  const desktopActionLinkClass =
    "inline-flex items-center text-sm uppercase tracking-wide text-site-nav transition-colors hover:text-site-green-dark";

  const iconButtonClass =
    "relative inline-flex items-center justify-center text-site-nav hover:text-site-green-dark transition-colors";

  const mobileItemClass = ({ isActive }: { isActive: boolean }) =>
    [
      "block w-full rounded-lg px-3 py-3 text-left text-[0.95rem] uppercase tracking-wide hover:bg-site-surface-soft transition-colors",
      isActive ? "font-semibold text-site-green-dark" : "text-site-nav hover:text-site-green-dark",
    ].join(" ");

  return (
    <>
      <div
        className="fixed left-0 top-0 z-[60] h-[3px] bg-site-green/55 transition-[width] duration-150"
        style={{ width: `${progress}%` }}
      />

      <header
        className={[
          "fixed inset-x-0 top-0 z-50 w-full backdrop-blur transition-all duration-300",
          isScrolled
            ? "bg-gradient-to-b from-site-surface/95 to-site-surface-soft/95 shadow-md"
            : "bg-white/95 shadow-sm",
        ].join(" ")}
      >
        <nav
          className={[
            "mx-auto flex w-full max-w-7xl items-center justify-between px-4 sm:px-6 lg:px-8",
            isScrolled ? "h-16" : "h-20",
          ].join(" ")}
          aria-label="Fő navigáció"
        >
          <Link
            to={ROUTES.home}
            onClick={goTop}
            aria-label="Kezdőlap"
            className="flex min-w-0 items-center gap-3"
          >
            <img
              src={logo}
              alt="Biogarden logó"
              className="h-12 w-auto sm:h-14"
            />
            <img
              src={bakonykincse}
              alt="Bakony Kincse Biokertészet"
              className="hidden h-10 w-auto xl:block"
            />
          </Link>

          <div className="hidden lg:flex items-center gap-8">
            <NavLink to={ROUTES.about} className={navLinkClass}>
              Rólunk
            </NavLink>

            <div className="group relative">
              <NavLink to={ROUTES.products} className={navLinkClass}>
                <span className="flex items-center gap-1">
                  Termékek
                  <svg
                    xmlns="http://www.w3.org/2000/svg"
                    className="h-4 w-4"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="1.8"
                  >
                    <path strokeLinecap="round" strokeLinejoin="round" d="m19.5 8.25-7.5 7.5-7.5-7.5" />
                  </svg>
                </span>
              </NavLink>

              <div className="invisible absolute left-0 top-full mt-3 w-72 rounded-2xl border border-site-border bg-white/95 p-2 opacity-0 shadow-xl transition-all duration-200 group-hover:visible group-hover:opacity-100">
                {PRODUCT_MENU_ITEMS.map((item) => (
                  <NavLink
                    key={item.to}
                    to={item.to}
                    className="block rounded-xl px-4 py-3 text-sm text-site-nav transition hover:bg-site-surface-soft hover:text-site-green-dark"
                  >
                    {item.label}
                  </NavLink>
                ))}
              </div>
            </div>

            <NavLink to={ROUTES.posts} className={navLinkClass}>
              Tudástár
            </NavLink>

            <button
              type="button"
              onClick={goContact}
className="inline-flex items-center text-[0.95rem] lg:text-base uppercase tracking-wide text-site-nav transition-colors hover:text-site-green-dark cursor-pointer"            >
              Kapcsolat
            </button>
          </div>

          <div className="hidden lg:flex items-center gap-5">
            {isAuthenticated ? (
              <div className="group relative py-4">
                <NavLink
                  to={ROUTES.account}
                  className={desktopActionLinkClass}
                  aria-label="Fiókom"
                  title="Fiókom"
                >
                  <span className="flex items-center gap-1">Fiókom <svg aria-hidden="true" className="h-3.5 w-3.5" viewBox="0 0 20 20" fill="currentColor"><path fillRule="evenodd" d="M5.23 7.21a.75.75 0 0 1 1.06.02L10 11.168l3.71-3.938a.75.75 0 1 1 1.08 1.04l-4.25 4.5a.75.75 0 0 1-1.08 0l-4.25-4.5a.75.75 0 0 1 .02-1.06Z" clipRule="evenodd"/></svg></span>
                </NavLink>
                <div className="invisible absolute right-0 top-full w-64 rounded-sm border border-site-border bg-white p-2 opacity-0 shadow-xl transition-all group-hover:visible group-hover:opacity-100 group-focus-within:visible group-focus-within:opacity-100">
                  <NavLink to={ROUTES.account} className="block rounded-sm px-4 py-3 text-sm text-site-nav hover:bg-site-surface-soft hover:text-site-green-dark">Adataim</NavLink>
                  {isAdmin && <NavLink to={ROUTES.admin} className="block rounded-sm px-4 py-3 text-sm text-site-nav hover:bg-site-surface-soft hover:text-site-green-dark">Adminisztrációs felület</NavLink>}
                  <button type="button" onClick={logout} className="block w-full rounded-sm px-4 py-3 text-left text-sm font-semibold text-site-danger hover:bg-site-danger-soft">Kijelentkezés</button>
                </div>
              </div>
            ) : (
              <>
                <NavLink
                  to={ROUTES.login}
                  className={desktopActionLinkClass}
                  aria-label="Belépés"
                  title="Belépés"
                >
                  Belépés
                </NavLink>

                <NavLink
                  to={ROUTES.register}
                  className={desktopActionLinkClass}
                  aria-label="Regisztráció"
                  title="Regisztráció"
                >
                  Regisztráció
                </NavLink>
              </>
            )}

            <NavLink
              to={ROUTES.cart}
              className={iconButtonClass}
              aria-label="Kosár"
              title="Kosár"
            >
              <svg xmlns="http://www.w3.org/2000/svg" width="26" height="26" viewBox="0 0 24 24" fill="currentColor">
                <path d="M16.53 13H8.118L6.167 7H18.67l-2.14 6ZM19 5H5.517l-.59-1.815A1 1 0 0 0 3.976 2.5H2a1 1 0 0 0 0 2h1.25l3.169 9.748A1 1 0 0 0 7.37 15H17.2a1 1 0 0 0 .942-.664l2.8-8A1 1 0 0 0 20 5Zm-10 14a1.75 1.75 0 1 0 .001 3.501A1.75 1.75 0 0 0 9 19Zm8 0a1.75 1.75 0 1 0 .001 3.501A1.75 1.75 0 0 0 17 19Z" />
              </svg>

              {cartItemCount > 0 && (
                <span className="absolute -right-2 -top-2 inline-flex min-h-5 min-w-5 items-center justify-center rounded-full bg-site-danger px-1 text-[11px] font-semibold text-white">
                  {cartItemCount}
                </span>
              )}
            </NavLink>
          </div>

          <button
            type="button"
            aria-label="Menü megnyitása"
            aria-expanded={mobileOpen}
            aria-controls="mobile-menu"
            className="inline-flex items-center gap-2 rounded-lg px-3 py-2 text-site-nav lg:hidden"
            onClick={() => setMobileOpen((prev) => !prev)}
          >
            <svg xmlns="http://www.w3.org/2000/svg" className="h-5 w-5" viewBox="0 0 20 20" fill="currentColor">
              <path d="M2 4h16v2H2V4Zm0 5h16v2H2V9Zm0 5h16v2H2v-2Z" />
            </svg>
            <span className="text-sm font-medium uppercase tracking-wide">Menü</span>
          </button>
        </nav>

        <div
          id="mobile-menu"
          className={[
            "border-t border-site-border lg:hidden",
            mobileOpen ? "block" : "hidden",
            isScrolled ? "bg-site-surface/95" : "bg-white",
          ].join(" ")}
        >
          <div className="mx-auto flex max-w-7xl flex-col gap-1 px-4 py-4 sm:px-6">
            <NavLink to={ROUTES.about} className={mobileItemClass} onClick={closeMobile}>
              Rólunk
            </NavLink>

            <NavLink to={ROUTES.products} className={mobileItemClass} onClick={closeMobile}>
              Termékek
            </NavLink>

            <div className="rounded-xl bg-site-surface-soft px-3 py-2">
              <div className="mb-2 text-xs font-semibold uppercase tracking-wider text-site-muted">
                Termékkategóriák
              </div>

              <div className="flex flex-col">
                {PRODUCT_MENU_ITEMS.map((item) => (
                  <NavLink
                    key={item.to}
                    to={item.to}
                    className="rounded-lg px-2 py-2 text-sm text-site-nav hover:bg-white hover:text-site-green-dark"
                    onClick={closeMobile}
                  >
                    {item.label}
                  </NavLink>
                ))}
              </div>
            </div>

            <NavLink to={ROUTES.posts} className={mobileItemClass} onClick={closeMobile}>
              Tudástár
            </NavLink>

            <button
              type="button"
              onClick={goContact}
              className={mobileItemClass({ isActive: false })}
            >
              Kapcsolat
            </button>

            <div className="mt-2 grid grid-cols-2 gap-3">
              {isAuthenticated ? (
                <button
                  type="button"
                  onClick={() => setMobileAccountOpen((open) => !open)}
                  aria-expanded={mobileAccountOpen}
                  className="col-span-2 inline-flex items-center justify-center gap-2 rounded-xl border border-site-border bg-white px-4 py-3 text-sm uppercase tracking-wide text-site-nav transition-colors hover:text-site-green-dark"
                >
                  Fiókom <svg aria-hidden="true" className={`h-4 w-4 transition-transform ${mobileAccountOpen ? "rotate-180" : ""}`} viewBox="0 0 20 20" fill="currentColor"><path fillRule="evenodd" d="M5.23 7.21a.75.75 0 0 1 1.06.02L10 11.168l3.71-3.938a.75.75 0 1 1 1.08 1.04l-4.25 4.5a.75.75 0 0 1-1.08 0l-4.25-4.5a.75.75 0 0 1 .02-1.06Z" clipRule="evenodd"/></svg>
                </button>
              ) : (
                <>
                  <NavLink
                    to={ROUTES.login}
                    onClick={closeMobile}
                    className="inline-flex items-center justify-center rounded-xl border border-site-border bg-white px-4 py-3 text-sm uppercase tracking-wide text-site-nav transition-colors hover:text-site-green-dark"
                  >
                    Belépés
                  </NavLink>

                  <NavLink
                    to={ROUTES.register}
                    onClick={closeMobile}
                    className="inline-flex items-center justify-center rounded-xl border border-site-border bg-white px-4 py-3 text-sm uppercase tracking-wide text-site-nav transition-colors hover:text-site-green-dark"
                  >
                    Regisztráció
                  </NavLink>
                </>
              )}
            </div>

            {isAuthenticated && mobileAccountOpen && (
              <div className="grid gap-1 rounded-xl border border-site-border bg-white p-2 shadow-sm">
                <NavLink to={ROUTES.account} onClick={closeMobile} className="rounded-lg px-4 py-3 text-sm text-site-nav hover:bg-site-surface-soft hover:text-site-green-dark">Adataim</NavLink>
                {isAdmin && <NavLink to={ROUTES.admin} onClick={closeMobile} className="rounded-lg px-4 py-3 text-sm text-site-nav hover:bg-site-surface-soft hover:text-site-green-dark">Adminisztrációs felület</NavLink>}
                <button type="button" onClick={logout} className="rounded-lg px-4 py-3 text-left text-sm font-semibold text-site-danger hover:bg-site-danger-soft">Kijelentkezés</button>
              </div>
            )}

            <div className="mt-3">
              <NavLink
                to={ROUTES.cart}
                onClick={closeMobile}
                className="relative inline-flex w-full items-center justify-center gap-2 rounded-xl border border-site-border bg-white px-4 py-3 text-sm uppercase tracking-wide text-site-nav transition-colors hover:text-site-green-dark"
              >
                <svg xmlns="http://www.w3.org/2000/svg" width="22" height="22" viewBox="0 0 24 24" fill="currentColor">
                  <path d="M16.53 13H8.118L6.167 7H18.67l-2.14 6ZM19 5H5.517l-.59-1.815A1 1 0 0 0 3.976 2.5H2a1 1 0 0 0 0 2h1.25l3.169 9.748A1 1 0 0 0 7.37 15H17.2a1 1 0 0 0 .942-.664l2.8-8A1 1 0 0 0 20 5Zm-10 14a1.75 1.75 0 1 0 .001 3.501A1.75 1.75 0 0 0 9 19Zm8 0a1.75 1.75 0 1 0 .001 3.501A1.75 1.75 0 0 0 17 19Z" />
                </svg>
                Kosár

                {cartItemCount > 0 && (
                  <span className="absolute -right-2 -top-2 inline-flex min-h-5 min-w-5 items-center justify-center rounded-full bg-site-danger px-1 text-[11px] font-semibold text-white">
                    {cartItemCount}
                  </span>
                )}
              </NavLink>
            </div>

          </div>
        </div>
      </header>

      <div aria-hidden className={isScrolled ? "h-20" : "h-24"} />
    </>
  );
}
