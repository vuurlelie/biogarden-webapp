import { useEffect, useState } from "react";
import { Link, useLocation, useSearchParams } from "react-router";
import { getPublicOrderPaymentStatus } from "../../features/account/api/ordersApi";
import type { Order } from "../../features/account/types/order.types";
import { useAnalytics } from "../../features/analytics/context/AnalyticsContextCore";
import { ROUTES } from "../../shared/constants/routes";

type SumUpReturnState = "idle" | "checking" | "paid" | "failed" | "pending" | "error";

export default function OrderSuccessPage() {
  const order = (useLocation().state as { order?: Order } | null)?.order;
  const [searchParams] = useSearchParams();
  const sumUpCheckoutReference = searchParams.get("sumup_checkout_reference");
  const orderId = searchParams.get("orderId");
  const [sumUpReturnState, setSumUpReturnState] = useState<SumUpReturnState>("idle");
  const [returnedOrderNumber, setReturnedOrderNumber] = useState<string | null>(null);
  const { track } = useAnalytics();

  useEffect(() => {
    if (order) {
      track("payment_return_viewed", { status: "offline_or_confirmed" });
    }
  }, [order, track]);

  useEffect(() => {
    if (!sumUpCheckoutReference || !orderId) {
      return;
    }

    let cancelled = false;
    let attempts = 0;
    let timeoutId: number | undefined;

    async function checkPaymentStatus() {
      attempts += 1;
      setSumUpReturnState("checking");

      try {
        const status = await getPublicOrderPaymentStatus(orderId!, sumUpCheckoutReference!);
        if (cancelled) return;

        setReturnedOrderNumber(status.orderNumber);

        if (status.paymentStatus === 2) {
          setSumUpReturnState("paid");
          track("payment_return_viewed", { status: "paid" });
          return;
        }

        if (status.paymentStatus === 3) {
          setSumUpReturnState("failed");
          track("payment_return_viewed", { status: "failed" });
          return;
        }

        if (attempts < 8) {
          timeoutId = window.setTimeout(checkPaymentStatus, 2000);
          return;
        }

        setSumUpReturnState("pending");
        track("payment_return_viewed", { status: "pending" });
      } catch {
        if (!cancelled) {
          setSumUpReturnState("error");
          track("payment_return_viewed", { status: "error" });
        }
      }
    }

    void checkPaymentStatus();

    return () => {
      cancelled = true;
      if (timeoutId) window.clearTimeout(timeoutId);
    };
  }, [orderId, sumUpCheckoutReference, track]);

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
      ) : sumUpCheckoutReference ? (
        <>
          {sumUpReturnState === "paid" ? (
            <>
              <p className="mt-4 text-site-text">
                Az online bankkártyás fizetés sikeres volt, a rendelést megerősítettük.
              </p>
              <p className="mt-3 text-sm text-site-muted">
                A visszaigazolást elküldjük a megadott email címre.
              </p>
            </>
          ) : sumUpReturnState === "failed" ? (
            <>
              <p className="mt-4 text-site-text">
                A rendelést rögzítettük, de az online bankkártyás fizetés nem sikerült.
              </p>
              <Link
                to={`${ROUTES.checkout}?sumup=cancelled&orderId=${encodeURIComponent(orderId ?? "")}`}
                className="mt-6 inline-flex rounded-sm bg-site-green px-5 py-3 font-semibold text-white"
              >
                Fizetés újrapróbálása
              </Link>
            </>
          ) : (
            <>
              <p className="mt-4 text-site-text">
                Visszaérkeztél a weboldalra, ellenőrizzük a fizetés állapotát.
              </p>
              <p className="mt-3 text-sm text-site-muted">
                Ez általában néhány másodperc. Ha nem változik az állapot, kérjük, vedd fel velünk a kapcsolatot.
              </p>
            </>
          )}
          {(returnedOrderNumber || orderId) && (
            <p className="mt-4 text-sm text-site-muted">
              Rendelés azonosító: <span className="font-semibold">{returnedOrderNumber ?? orderId}</span>
            </p>
          )}
          {sumUpReturnState === "error" && (
            <div role="alert" className="mt-5 rounded-sm bg-site-danger-soft p-4 text-site-danger">
              A fizetés állapotát most nem sikerült ellenőrizni. Kérjük, vedd fel velünk a kapcsolatot.
            </div>
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
