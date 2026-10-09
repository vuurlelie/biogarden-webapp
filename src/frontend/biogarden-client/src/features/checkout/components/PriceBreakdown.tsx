import { PRICE } from "../utils/checkoutFormatting";

export type PriceSnapshot = { subtotal?: number | null; deliveryFee?: number | null; paymentFee?: number | null; totalPrice: number; currency?: string | null };
export function PriceBreakdown({ price }: { price: PriceSnapshot }) {
  const format = (value: number) => price.currency && price.currency !== "HUF"
    ? new Intl.NumberFormat(
      "hu-HU",
      { style: "currency", currency: price.currency }
    ).format(value) : PRICE.format(value);
  return <dl className="space-y-2" aria-label="Árösszesítés">
    {price.subtotal != null && <div className="flex justify-between gap-3"><dt>Termékek összege</dt><dd>{format(price.subtotal)}</dd></div>}
    {price.deliveryFee != null && <div className="flex justify-between gap-3"><dt>Szállítási díj</dt><dd>{format(price.deliveryFee)}</dd></div>}
    {price.paymentFee != null && <div className="flex justify-between gap-3"><dt>Fizetési díj</dt><dd>{format(price.paymentFee)}</dd></div>}
    <div className="flex justify-between gap-3 border-t border-site-border pt-2 font-bold"><dt>Végösszeg</dt><dd>{format(price.totalPrice)}</dd></div>
    {(price.subtotal == null || price.deliveryFee == null || price.paymentFee == null) && <div className="text-sm text-site-muted">A korábbi rendelés díjbontása nem áll rendelkezésre.</div>}
  </dl>;
}
