import { Link } from "react-router";
import { ROUTES } from "../../../shared/constants/routes";
import type { ProductCategoryFilter } from "../types/product.types";
import { PRODUCT_CATEGORY_OPTIONS } from "../utils/productFilters";

type ProductNavigationProps = {
  currentCategory?: Exclude<ProductCategoryFilter, "all">;
};

export default function ProductNavigation({
  currentCategory,
}: ProductNavigationProps) {
  const categoryLabel = currentCategory
    ? PRODUCT_CATEGORY_OPTIONS.find((option) => option.value === currentCategory)?.label
    : null;

  return (
    <nav className="flex flex-wrap items-center gap-2 text-sm text-site-text" aria-label="Morzsamenü">
      <Link
        to={ROUTES.home}
        className="font-bold text-site-green-dark hover:underline"
      >
        Kezdőlap
      </Link>
      <span aria-hidden="true">/</span>

      {categoryLabel ? (
        <>
          <Link
            to={ROUTES.products}
            className="font-bold text-site-green-dark hover:underline"
          >
            Termékeink
          </Link>
          <span aria-hidden="true">/</span>
          <span className="text-site-heading" aria-current="page">
            {categoryLabel}
          </span>
        </>
      ) : (
        <span className="text-site-heading" aria-current="page">
          Termékeink
        </span>
      )}
    </nav>
  );
}
