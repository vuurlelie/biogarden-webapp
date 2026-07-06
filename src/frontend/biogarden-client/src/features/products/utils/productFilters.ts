import type {
  ProductCategory,
  ProductCategoryFilter,
} from "../types/product.types";

export const PRODUCT_CATEGORY_OPTIONS: Array<{
  value: ProductCategoryFilter;
  label: string;
}> = [
  { value: "all", label: "Összes termék" },
  { value: "zoldsegek", label: "Zöldségek" },
  {
    value: "fuszerek-gyogyteak",
    label: "Fűszerek, gyógyteák",
  },
  {
    value: "illoolajok-hidrolatumok",
    label: "Illóolajok, hidrolátumok",
  },
];

const CATEGORY_GROUPS: Record<
  Exclude<ProductCategoryFilter, "all">,
  ProductCategory[]
> = {
  zoldsegek: [0],
  "fuszerek-gyogyteak": [2, 5],
  "illoolajok-hidrolatumok": [3, 4],
};

export function isProductCategoryFilter(
  value: string | null,
): value is ProductCategoryFilter {
  return PRODUCT_CATEGORY_OPTIONS.some((option) => option.value === value);
}

export function matchesProductCategory(
  category: ProductCategory,
  filter: ProductCategoryFilter,
): boolean {
  return filter === "all" || CATEGORY_GROUPS[filter].includes(category);
}
