import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router";
import { createSumUpCheckout, getCheckoutOrder, recoverCheckoutOrder } from "../../account/api/ordersApi";
import type { Order } from "../../account/types/order.types";
import { getOrderStatusLabel, getPaymentStatusLabel } from "../../account/utils/orderLabels";
import { ROUTES } from "../../../shared/constants/routes";
import { getErrorMessage, hasErrorStatus } from "../../../shared/utils/errorHandling";
import { PAYMENT } from "../constants/payment";

const CANCELLED = 4;
const PAID = 2;
const PENDING = 0;
const FAILED = 3;

export function CheckoutRecoveryView({ orderId, checkoutKey, busy, error: initialError }: {
  orderId: string | null; checkoutKey: string | null; busy: boolean; error: string;
}) {
  const navigate = useNavigate();
  const [order, setOrder] = useState<Order | null>(null);
  const [loading, setLoading] = useState(true);
  const [paying, setPaying] = useState(false);
  const [error, setError] = useState("");
  const [revision, setRevision] = useState(0);
  const headingRef = useRef<HTMLHeadingElement>(null);
  useEffect(
    () => { headingRef.current?.focus(); },
    [orderId, checkoutKey]
  );

  useEffect(() => {
    if (busy)
      return;
    const controller = new AbortController();
    const request = orderId ? getCheckoutOrder(orderId, controller.signal)
      : recoverCheckoutOrder(checkoutKey!, controller.signal);
    void request.then(value => {
      if (controller.signal.aborted)
        return;
      setOrder(value);
      setError("");
      if (value.status !== CANCELLED && (value.paymentStatus === PAID || (value.paymentMethod !== null && value.paymentMethod !== PAYMENT.OnlineCard))) {
        navigate(`${ROUTES.orderSuccess}?orderId=${encodeURIComponent(value.id)}`, { replace: true, state: { order: value } });
      }
    }).catch(reason => {
      if (controller.signal.aborted)
        return;
      setOrder(null);
      setError(hasErrorStatus(reason, 404)
        ? "A rendelés ebben a munkamenetben még nem található. Ellenőrizd újra az eredményt; ha kijelentkeztél, jelentkezz vissza."
        : getErrorMessage(reason, "A rendelés állapotát most nem tudjuk ellenőrizni."));
    }).finally(() => {
      if (!controller.signal.aborted)
        setLoading(false);
    });
    return () => controller.abort();
  }, [orderId, checkoutKey, busy, revision, navigate]);

  async function retryPayment() {
    if (!order || paying)
      return;
    setPaying(true);
    setError("");
    try {
      const checkout = await createSumUpCheckout(order.id);
      window.location.assign(checkout.url);
    } catch (reason) {
      setError(getErrorMessage(reason, "A fizetés újraindítása nem sikerült."));
      // A callback may have confirmed payment while this request was in flight.
      if (hasErrorStatus(reason, 409)) {
        setLoading(true);
        setRevision(value => value + 1);
      }
    } finally { setPaying(false); }
  }

  const canPay = order && order.status !== CANCELLED && (order.paymentMethod === null || order.paymentMethod === PAYMENT.OnlineCard)
    && (order.paymentStatus === PENDING || order.paymentStatus === FAILED);
  return <div className="mx-auto max-w-2xl px-4 py-16 text-center">
    <h1 ref={headingRef} tabIndex={-1} className="text-3xl font-bold text-site-heading">Rendelés és fizetés ellenőrzése</h1>
    <p className="mt-4">Ne küldj új rendelést ugyanazokra a termékekre. Itt a korábbi rendelés eredményét ellenőrizheted.</p>
    {busy || loading ? <p role="status" className="mt-4">Az eredmény ellenőrzése folyamatban...</p> : <>
      {order && <div className="mt-4">
        <p>Rendelésszám: <strong>{order.orderNumber}</strong></p>
        <p>{getOrderStatusLabel(order.status)} · {getPaymentStatusLabel(order.paymentStatus)}</p>
      </div>}
      {(error || initialError) && <p role="alert" className="mt-4 text-site-danger">{error || initialError}</p>}
      <div className="mt-6 flex flex-wrap justify-center gap-4">
        {canPay && <button type="button" disabled={paying} onClick={() => void retryPayment()}
          className="rounded-sm bg-site-green px-5 py-3 font-semibold text-white disabled:opacity-50">
          {paying ? "Átirányítás..." : "Fizetés újrapróbálása"}
        </button>}
        <button type="button" disabled={paying} onClick={() => { setLoading(true); setRevision(value => value + 1); }}
          className="rounded-sm border border-site-border px-5 py-3">Állapot ellenőrzése</button>
        <a href="mailto:info@bakonykincse.hu" className="px-5 py-3 underline">Segítséget kérek</a>
      </div>
    </>}
  </div>;
}
