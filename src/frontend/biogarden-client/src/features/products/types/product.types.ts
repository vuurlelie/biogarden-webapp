export type ProductCategory = 0 | 2 | 3 | 4 | 5;

export type UnitOfMeasure = 0 | 1 | 2 | 3 | 4 | 5 | 6;

export type ProductCategoryFilter =
  | "all"
  | "zoldsegek"
  | "fuszerek-gyogyteak"
  | "illoolajok-hidrolatumok";

export type ProductSortOrder = "abc" | "price-asc" | "price-desc";

export type ProductListItem = {
  id: number;
  name: string;
  price: number;
  category: ProductCategory;
  image: string;
  unit: UnitOfMeasure;
  unitAmount: number | null;
  unitAmountMeasure: UnitOfMeasure | null;
  available: boolean;
  deliveryAvailable: boolean;
};
