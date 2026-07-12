import { Link, useLocation, useSearchParams } from "react-router-dom";
import type { Order } from "../../features/account/types/order.types";
import { ROUTES } from "../../shared/constants/routes";

export default function OrderSuccessPage() {
  const order = (useLocation().state as { order?: Order } | null)?.order;
  const [searchParams] = useSearchParams();
  const stripeSessionId = searchParams.get("stripe_session_id");
  const orderId = searchParams.get("orderId");

  return (
    <main className="mx-auto max-w-2xl px-4 py-16 text-center">
      <h1 className="text-3xl font-bold text-site-heading">Köszönjük a rendelést!</h1>

      {order ? (
        <>
          <p className="mt-4 text-site-text">A rendelés azonosítója:</p>
          <p className="mt-2 text-xl font-bold text-site-green-dark">{order.orderNumber}</p>
          <p className="mt-4 text-sm text-site-text">
            A visszaigazolást elküldjük a(z) {order.customerEmail} címre.
          </p>
        </>
      ) : stripeSessionId ? (
        <>
          <p className="mt-4 text-site-text">
            Az online fizetés sikeresen visszairányított minket a Stripe oldaláról.
          </p>
          <p className="mt-3 text-sm text-site-muted">
            A fizetés véglegesítését a Stripe webhook erősíti meg. Ha néhány percen belül nem érkezik
            visszaigazolás, kérjük, vedd fel velünk a kapcsolatot.
          </p>
          {orderId && (
            <p className="mt-4 text-sm text-site-muted">
              Rendelés azonosító: <span className="font-semibold">{orderId}</span>
            </p>
          )}
        </>
      ) : (
        <p className="mt-4 text-site-text">A rendelést sikeresen rögzítettük.</p>
      )}

      <Link
        to={ROUTES.products}
        className="mt-8 inline-flex rounded-sm bg-site-green px-5 py-3 font-semibold text-white"
      >
        Vissza a termékekhez
      </Link>
    </main>
  );
}
