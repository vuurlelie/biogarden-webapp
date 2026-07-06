import type {
  ProductCategoryFilter,
  ProductSortOrder,
} from "../types/product.types";
import { PRODUCT_CATEGORY_OPTIONS } from "../utils/productFilters";

type ProductControlsProps = {
  category: ProductCategoryFilter;
  sortOrder: ProductSortOrder;
  availableOnly: boolean;
  deliveryOnly: boolean;
  onCategoryChange: (value: ProductCategoryFilter) => void;
  onSortChange: (value: ProductSortOrder) => void;
  onAvailableOnlyChange: (value: boolean) => void;
  onDeliveryOnlyChange: (value: boolean) => void;
};

export default function ProductControls({
  category,
  sortOrder,
  availableOnly,
  deliveryOnly,
  onCategoryChange,
  onSortChange,
  onAvailableOnlyChange,
  onDeliveryOnlyChange,
}: ProductControlsProps) {
  return (
    <div className="mb-6 grid gap-4 rounded-sm border border-site-border bg-site-surface-soft p-4 lg:grid-cols-[1fr_auto] lg:items-start">
      <div className="grid gap-4 sm:grid-cols-2">
        <label className="flex flex-col gap-2 text-sm font-semibold text-site-heading">
          Kategória
          <select
            value={category}
            onChange={(event) =>
              onCategoryChange(event.target.value as ProductCategoryFilter)
            }
            className="rounded-sm border border-site-border bg-white px-3 py-2 font-normal text-site-text focus:border-site-green focus:outline-none focus:ring-2 focus:ring-site-green/15"
          >
            {PRODUCT_CATEGORY_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </label>

        <label className="flex flex-col gap-2 text-sm font-semibold text-site-heading">
          Rendezés
          <select
            value={sortOrder}
            onChange={(event) =>
              onSortChange(event.target.value as ProductSortOrder)
            }
            className="rounded-sm border border-site-border bg-white px-3 py-2 font-normal text-site-text focus:border-site-green focus:outline-none focus:ring-2 focus:ring-site-green/15"
          >
            <option value="abc">ABC szerint növekvő</option>
            <option value="price-asc">Ár szerint növekvő</option>
            <option value="price-desc">Ár szerint csökkenő</option>
          </select>
        </label>
      </div>

      <div className="flex flex-col gap-3 text-sm text-site-text sm:flex-row sm:flex-wrap lg:mt-7 lg:min-h-10 lg:items-center lg:justify-end">
        <label className="inline-flex cursor-pointer items-center gap-2">
          <input
            type="checkbox"
            checked={deliveryOnly}
            onChange={(event) => onDeliveryOnlyChange(event.target.checked)}
            className="h-4 w-4 accent-site-green"
          />
          Házhozszállítás lehetséges
        </label>

        <label className="inline-flex cursor-pointer items-center gap-2">
          <input
            type="checkbox"
            checked={availableOnly}
            onChange={(event) => onAvailableOnlyChange(event.target.checked)}
            className="h-4 w-4 accent-site-green"
          />
          Csak az elérhető termékek
        </label>
      </div>
    </div>
  );
}
