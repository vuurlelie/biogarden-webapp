import { useEffect, useMemo, useState } from "react";
import { Link, NavLink } from "react-router-dom";
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
  { label: "Fűszerek, zöldségek", to: `${ROUTES.products}?category=fuszerek-zoldsegek` },
  { label: "Gyógynövények, gyógyteák", to: `${ROUTES.products}?category=gyogynovenyek-gyogyteak` },
  { label: "Illóolajok, hidrolátumok", to: `${ROUTES.products}?category=illoolajok-hidrolatumok` },
];

export default function Navbar({
  isAuthenticated = false,
  isAdmin = false,
  cartItemCount = 0,
}: NavbarProps) {
  const [isScrolled, setIsScrolled] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
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

  const closeMobile = () => setMobileOpen(false);

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
          "inline-flex items-center text-[0.95rem] lg:text-base uppercase tracking-wide transition-colors",
          isActive ? "font-semibold text-[#5f761d]" : "text-[#6F8625] hover:text-[#5f761d]",
        ].join(" "),
    [],
  );

  const iconButtonClass =
    "relative inline-flex items-center justify-center text-[#6F8625] hover:text-[#5f761d] transition-colors";

  const mobileItemClass =
    "block w-full rounded-lg px-3 py-3 text-left text-[0.95rem] uppercase tracking-wide text-[#6F8625] hover:bg-[#f6edd8] hover:text-[#5f761d] transition-colors";

  return (
    <>
      <div
        className="fixed left-0 top-0 z-[60] h-[3px] bg-[#6F8625]/55 transition-[width] duration-150"
        style={{ width: `${progress}%` }}
      />

      <header
        className={[
          "fixed inset-x-0 top-0 z-50 w-full backdrop-blur transition-all duration-300",
          isScrolled
            ? "bg-gradient-to-b from-[#FFF3EA]/95 to-[#FFE8B2]/95 shadow-md"
            : "bg-[#FBFBFB]/95 shadow-sm",
        ].join(" ")}
      >
        <nav
          className={[
            "mx-auto flex w-full max-w-7xl items-center justify-between px-4 sm:px-6 lg:px-8",
            isScrolled ? "h-20" : "h-24",
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
              alt="Biogarden"
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

              <div className="invisible absolute left-0 top-full mt-3 w-72 rounded-2xl border border-[#ead8a9] bg-white/95 p-2 opacity-0 shadow-xl transition-all duration-200 group-hover:visible group-hover:opacity-100">
                {PRODUCT_MENU_ITEMS.map((item) => (
                  <NavLink
                    key={item.to}
                    to={item.to}
                    className="block rounded-xl px-4 py-3 text-sm text-[#5c6c30] transition hover:bg-[#f7efd8] hover:text-[#49561d]"
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
              className="inline-flex items-center text-[0.95rem] lg:text-base uppercase tracking-wide text-[#6F8625] transition-colors hover:text-[#5f761d]"
            >
              Kapcsolat
            </button>
          </div>

          <div className="hidden lg:flex items-center gap-5">
            <NavLink
              to={isAuthenticated ? ROUTES.account : ROUTES.login}
              className={iconButtonClass}
              aria-label={isAuthenticated ? "Fiókom" : "Belépés"}
              title={isAuthenticated ? "Fiókom" : "Belépés"}
            >
              <div className="flex items-center gap-2">
                <svg xmlns="http://www.w3.org/2000/svg" width="30" height="30" viewBox="0 0 24 24" fill="currentColor">
                  <path d="M12 2C9.243 2 7 4.243 7 7s2.243 5 5 5 5-2.243 5-5S14.757 2 12 2Zm0 8c-1.654 0-3-1.346-3-3s1.346-3 3-3 3 1.346 3 3-1.346 3-3 3Zm0 3c-4.411 0-8 3.589-8 8a1 1 0 0 0 2 0c0-3.309 2.691-6 6-6s6 2.691 6 6a1 1 0 0 0 2 0c0-4.411-3.589-8-8-8Z" />
                </svg>
                <span className="text-sm uppercase tracking-wide">
                  {isAuthenticated ? "Fiókom" : "Belépés"}
                </span>
              </div>
            </NavLink>

            {isAuthenticated && isAdmin && (
              <NavLink
                to={ROUTES.admin}
                className="text-sm uppercase tracking-wide text-[#6F8625] transition-colors hover:text-[#5f761d]"
              >
                Admin
              </NavLink>
            )}

            <NavLink
              to={ROUTES.cart}
              className={iconButtonClass}
              aria-label="Kosár"
              title="Kosár"
            >
              <svg xmlns="http://www.w3.org/2000/svg" width="30" height="30" viewBox="0 0 24 24" fill="currentColor">
                <path d="M16.53 13H8.118L6.167 7H18.67l-2.14 6ZM19 5H5.517l-.59-1.815A1 1 0 0 0 3.976 2.5H2a1 1 0 0 0 0 2h1.25l3.169 9.748A1 1 0 0 0 7.37 15H17.2a1 1 0 0 0 .942-.664l2.8-8A1 1 0 0 0 20 5Zm-10 14a1.75 1.75 0 1 0 .001 3.501A1.75 1.75 0 0 0 9 19Zm8 0a1.75 1.75 0 1 0 .001 3.501A1.75 1.75 0 0 0 17 19Z" />
              </svg>

              {cartItemCount > 0 && (
                <span className="absolute -right-2 -top-2 inline-flex min-h-5 min-w-5 items-center justify-center rounded-full bg-red-500 px-1 text-[11px] font-semibold text-white">
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
            className="inline-flex items-center gap-2 rounded-lg px-3 py-2 text-[#5a642e] lg:hidden"
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
            "border-t border-[#ead8a9] lg:hidden",
            mobileOpen ? "block" : "hidden",
            isScrolled ? "bg-[#FFF3EA]/95" : "bg-white",
          ].join(" ")}
        >
          <div className="mx-auto flex max-w-7xl flex-col gap-1 px-4 py-4 sm:px-6">
            <NavLink to={ROUTES.about} className={mobileItemClass} onClick={closeMobile}>
              Rólunk
            </NavLink>

            <NavLink to={ROUTES.products} className={mobileItemClass} onClick={closeMobile}>
              Termékek
            </NavLink>

            <div className="rounded-xl bg-[#fbf5e5] px-3 py-2">
              <div className="mb-2 text-xs font-semibold uppercase tracking-wider text-[#7d845c]">
                Termékkategóriák
              </div>

              <div className="flex flex-col">
                {PRODUCT_MENU_ITEMS.map((item) => (
                  <NavLink
                    key={item.to}
                    to={item.to}
                    className="rounded-lg px-2 py-2 text-sm text-[#5c6c30] hover:bg-white"
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
              className={mobileItemClass}
            >
              Kapcsolat
            </button>

            <div className="mt-2 grid grid-cols-2 gap-3">
              <NavLink
                to={isAuthenticated ? ROUTES.account : ROUTES.login}
                onClick={closeMobile}
                className="inline-flex items-center justify-center gap-2 rounded-xl border border-[#d8d1b0] bg-white px-4 py-3 text-sm uppercase tracking-wide text-[#5f761d]"
              >
                <svg xmlns="http://www.w3.org/2000/svg" width="22" height="22" viewBox="0 0 24 24" fill="currentColor">
                  <path d="M12 2C9.243 2 7 4.243 7 7s2.243 5 5 5 5-2.243 5-5S14.757 2 12 2Zm0 8c-1.654 0-3-1.346-3-3s1.346-3 3-3 3 1.346 3 3-1.346 3-3 3Zm0 3c-4.411 0-8 3.589-8 8a1 1 0 0 0 2 0c0-3.309 2.691-6 6-6s6 2.691 6 6a1 1 0 0 0 2 0c0-4.411-3.589-8-8-8Z" />
                </svg>
                {isAuthenticated ? "Fiókom" : "Belépés"}
              </NavLink>

              <NavLink
                to={ROUTES.cart}
                onClick={closeMobile}
                className="relative inline-flex items-center justify-center gap-2 rounded-xl border border-[#d8d1b0] bg-white px-4 py-3 text-sm uppercase tracking-wide text-[#5f761d]"
              >
                <svg xmlns="http://www.w3.org/2000/svg" width="22" height="22" viewBox="0 0 24 24" fill="currentColor">
                  <path d="M16.53 13H8.118L6.167 7H18.67l-2.14 6ZM19 5H5.517l-.59-1.815A1 1 0 0 0 3.976 2.5H2a1 1 0 0 0 0 2h1.25l3.169 9.748A1 1 0 0 0 7.37 15H17.2a1 1 0 0 0 .942-.664l2.8-8A1 1 0 0 0 20 5Zm-10 14a1.75 1.75 0 1 0 .001 3.501A1.75 1.75 0 0 0 9 19Zm8 0a1.75 1.75 0 1 0 .001 3.501A1.75 1.75 0 0 0 17 19Z" />
                </svg>
                Kosár

                {cartItemCount > 0 && (
                  <span className="absolute -right-2 -top-2 inline-flex min-h-5 min-w-5 items-center justify-center rounded-full bg-red-500 px-1 text-[11px] font-semibold text-white">
                    {cartItemCount}
                  </span>
                )}
              </NavLink>
            </div>

            {isAuthenticated && isAdmin && (
              <NavLink
                to={ROUTES.admin}
                onClick={closeMobile}
                className="mt-2 rounded-xl bg-[#6F8625] px-4 py-3 text-center text-sm font-medium uppercase tracking-wide text-white"
              >
                Admin felület
              </NavLink>
            )}
          </div>
        </div>
      </header>

      <div aria-hidden className={isScrolled ? "h-20" : "h-24"} />
    </>
  );
}