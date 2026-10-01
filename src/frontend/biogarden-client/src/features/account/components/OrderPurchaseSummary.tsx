import type { Order } from "../types/order.types";
import { PriceBreakdown } from "../../checkout/components/PriceBreakdown";
import { FULFILLMENT, FULFILLMENT_LABELS } from "../../checkout/constants/fulfillment";
import { PAYMENT_LABELS } from "../../checkout/constants/payment";

export function OrderPurchaseSummary({ order }: { order: Order }) {
  return <section aria-label="Mentett rendelési összesítés" className="my-5 space-y-3 rounded-sm bg-site-surface-soft p-4 text-left text-sm">
    <PriceBreakdown price={order} />
    <p><strong>Fizetési mód: </strong>{order.paymentMethod == null ? "Korábbi rendelés – nincs rögzítve" : PAYMENT_LABELS[order.paymentMethod]}</p>
    <p><strong>Átvétel: </strong>{FULFILLMENT_LABELS[order.fulfillmentMethod]}</p>
    {order.glsDeliveryPointId && <p><strong>{order.glsDeliveryPointName}</strong> ({order.glsDeliveryPointId})</p>}
    {order.fulfillmentMethod !== FULFILLMENT.PersonalPickup && <p>{[order.deliveryPostalCode, order.deliveryCity,
      order.glsDeliveryPointAddress ?? order.deliveryStreet, order.glsDeliveryPointAddress ? null : order.deliveryHouseNumber].filter(Boolean).join(" ")}</p>}
  </section>;
}
