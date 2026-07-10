import { Navigate, Outlet, useLocation } from "react-router-dom";
import { getAuthSession } from "../../features/auth/utils/authStorage";
import { ROUTES } from "../../shared/constants/routes";

export default function AdminRoute() {
  const location = useLocation();
  const auth = getAuthSession();

  if (!auth) {
    return (
      <Navigate
        to={ROUTES.adminLogin}
        replace
        state={{ from: `${location.pathname}${location.search}${location.hash}` }}
      />
    );
  }

  const isAdmin = auth.roles.some((role) => role.toLowerCase() === "admin");
  return isAdmin ? <Outlet /> : <AdminForbidden />;
}

function AdminForbidden() {
  return (
    <main className="min-h-screen bg-site-surface-soft px-4 py-12">
      <section className="mx-auto max-w-lg rounded-sm border border-site-border bg-white p-6 text-center shadow-lg sm:p-8">
        <p className="text-xs font-bold uppercase tracking-[0.16em] text-site-danger">
          Nincs jogosultság
        </p>
        <h1 className="mt-2 text-3xl font-bold text-site-heading">Admin hozzáférés szükséges</h1>
        <p className="mt-3 text-sm leading-6 text-site-text">
          Ehhez az oldalhoz admin jogosultság kell. Ha másik fiókkal szeretnél belépni, jelentkezz ki, majd lépj be admin fiókkal.
        </p>
        <div className="mt-6 flex flex-col gap-3 sm:flex-row sm:justify-center">
          <a href={ROUTES.home} className="rounded-sm border border-site-border px-4 py-2.5 text-sm font-semibold text-site-nav hover:bg-site-surface-soft">
            Vissza a weboldalra
          </a>
          <a href={ROUTES.account} className="rounded-sm bg-site-green px-4 py-2.5 text-sm font-semibold text-white hover:bg-site-green-hover">
            Saját fiókom
          </a>
        </div>
      </section>
    </main>
  );
}
