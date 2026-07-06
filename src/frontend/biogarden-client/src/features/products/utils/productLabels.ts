import type {
  ProductCategory,
  ProductListItem,
  UnitOfMeasure,
} from "../types/product.types";

const CATEGORY_LABELS: Record<ProductCategory, string> = {
  0: "Zöldség",
  2: "Gyógytea",
  3: "Illóolaj",
  4: "Hidrolátum",
  5: "Fűszer",
};

const UNIT_LABELS: Record<UnitOfMeasure, string> = {
  0: "db",
  1: "kg",
  2: "g",
  3: "ml",
  4: "l",
  5: "csomag",
  6: "üveg",
};

const PRICE_FORMATTER = new Intl.NumberFormat("hu-HU", {
  maximumFractionDigits: 2,
});

export function getProductCategoryLabel(category: ProductCategory): string {
  return CATEGORY_LABELS[category];
}

export function getUnitLabel(unit: UnitOfMeasure): string {
  return UNIT_LABELS[unit];
}

export function getProductPackageLabel(
  product: ProductListItem,
): string | null {
  if (
    product.unitAmount === null ||
    product.unitAmountMeasure === null
  ) {
    return null;
  }

  return `${PRICE_FORMATTER.format(product.unitAmount)} ${getUnitLabel(product.unitAmountMeasure)}`;
}

export function formatProductPrice(product: ProductListItem): string {
  return `${PRICE_FORMATTER.format(product.price)} Ft / ${getUnitLabel(product.unit)}`;
}
