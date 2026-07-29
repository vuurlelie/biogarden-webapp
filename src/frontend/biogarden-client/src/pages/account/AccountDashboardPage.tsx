import { Link } from "react-router";
import { getAuthSession } from "../../features/auth/utils/authStorage";
import { ROUTES } from "../../shared/constants/routes";

export default function AccountDashboardPage() {
  const auth = getAuthSession();

  return (
    <div className="rounded-sm border border-site-border bg-white p-6 shadow-lg sm:p-8">
      <p className="text-xs font-bold uppercase tracking-[0.15em] text-site-green-dark">
        Vásárlói fiók
      </p>
      <h1 className="mt-2 text-3xl font-bold text-site-heading">Üdvözlünk!</h1>
      <p className="mt-3 max-w-2xl leading-7 text-site-text">
        Bejelentkezve mint <span className="font-semibold text-site-heading">{auth?.email}</span>.
        Innen kezelheted a személyes adataidat, mentett címeidet és rendeléseidet.
      </p>

      <div className="mt-8 grid gap-4 sm:grid-cols-2">
        <Link
          to={ROUTES.accountProfile}
          className="group rounded-sm border border-site-border bg-site-surface p-5 transition hover:border-site-green hover:shadow-md"
        >
          <h2 className="text-lg font-bold text-site-heading group-hover:text-site-green-dark">
            Személyes adatok
          </h2>
          <p className="mt-2 text-sm leading-6 text-site-text">
            Név és telefonszám megadása vagy módosítása.
          </p>
        </Link>

        <Link
          to={ROUTES.accountAddresses}
          className="group rounded-sm border border-site-border bg-site-surface p-5 transition hover:border-site-green hover:shadow-md"
        >
          <h2 className="text-lg font-bold text-site-heading group-hover:text-site-green-dark">
            Mentett címek
          </h2>
          <p className="mt-2 text-sm leading-6 text-site-text">
            Szállítási és számlázási címek kezelése.
          </p>
        </Link>

        <Link
          to={ROUTES.accountOrders}
          className="group rounded-sm border border-site-border bg-site-surface p-5 transition hover:border-site-green hover:shadow-md"
        >
          <h2 className="text-lg font-bold text-site-heading group-hover:text-site-green-dark">
            Rendeléseim
          </h2>
          <p className="mt-2 text-sm leading-6 text-site-text">
            Korábbi rendelések és aktuális állapotuk.
          </p>
        </Link>

        <Link
          to={ROUTES.products}
          className="group rounded-sm border border-site-border bg-site-surface p-5 transition hover:border-site-green hover:shadow-md"
        >
          <h2 className="text-lg font-bold text-site-heading group-hover:text-site-green-dark">
            Termékek böngészése
          </h2>
          <p className="mt-2 text-sm leading-6 text-site-text">
            Tekintsd meg az aktuálisan elérhető termékeinket.
          </p>
        </Link>
      </div>
    </div>
  );
}
