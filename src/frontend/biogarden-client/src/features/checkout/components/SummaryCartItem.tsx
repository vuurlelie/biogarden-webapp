import type { CartItem } from "../../cart/types/cart.types";
import { getUnitLabel } from "../../products/utils/productLabels";
import { PRICE } from "../utils/checkoutFormatting";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

type SummaryCartItemProps = {
  item: Pick<CartItem, "productName" | "unitPrice" | "quantity" | "unit" | "lineTotal"> & Partial<Pick<CartItem, "image" | "productDescription">>;
  compact?: boolean;
};

function getCartImageUrl(image: string | null | undefined): string | null {
  return image ? `${API_BASE_URL}/${image.replace(/^\/+/, "")}` : null;
}

export function SummaryCartItem({ item, compact = false }: SummaryCartItemProps) {
  const imageUrl = getCartImageUrl(item.image);
  const description = item.productDescription?.trim();

  return (
    <li className="grid grid-cols-[4.25rem_minmax(0,1fr)_auto] gap-3 py-3 text-sm first:pt-0 last:pb-0">
      <div className="relative h-16 w-16 shrink-0 overflow-visible">
        <div className="h-full w-full overflow-hidden rounded-sm bg-white">
          {imageUrl ? (
            <img src={imageUrl} alt={item.productName} className="h-full w-full object-cover" />
          ) : (
            <div className="flex h-full w-full items-center justify-center text-[0.65rem] text-site-muted">
              Nincs kép
            </div>
          )}
        </div>
        <span className="absolute -right-2 -top-2 flex h-7 min-w-7 items-center justify-center rounded-full border border-site-border bg-white px-2 text-sm font-bold text-site-heading shadow-sm">
          {item.quantity}
        </span>
      </div>

      <div className="min-w-0">
        <h3 className="font-bold leading-snug text-site-heading">{item.productName}</h3>
        <p className="mt-1 text-site-text">
          {PRICE.format(item.unitPrice)} / {getUnitLabel(item.unit)}
        </p>
        {description && !compact && (
          <p className="mt-1 max-h-10 overflow-hidden text-site-muted">{description}</p>
        )}
      </div>

      <strong className="whitespace-nowrap text-right text-site-heading">{PRICE.format(item.lineTotal)}</strong>
    </li>
  );
}
