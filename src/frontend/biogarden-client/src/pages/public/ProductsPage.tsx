import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { getProductPage } from "../../features/products/api/productsApi";
import ProductCards from "../../features/products/components/ProductCards";
import ProductControls from "../../features/products/components/ProductControls";
import ProductNavigation from "../../features/products/components/ProductNavigation";
import ProductPagination from "../../features/products/components/ProductPagination";
import type {
  ProductCategoryFilter,
  ProductListItem,
  ProductSortOrder,
} from "../../features/products/types/product.types";
import { isProductCategoryFilter } from "../../features/products/utils/productFilters";
import Seo from "../../shared/seo/Seo";

const PRODUCTS_PER_PAGE = 12;

export default function ProductsPage() {
  const [products, setProducts] = useState<ProductListItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [hasError, setHasError] = useState(false);
  const [sortOrder, setSortOrder] = useState<ProductSortOrder>("abc");
  const [availableOnly, setAvailableOnly] = useState(false);
  const [deliveryOnly, setDeliveryOnly] = useState(false);
  const [currentPage, setCurrentPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(1);
  const [searchParams, setSearchParams] = useSearchParams();

  const categoryParam = searchParams.get("category");
  const selectedCategory: ProductCategoryFilter = isProductCategoryFilter(
    categoryParam,
  )
    ? categoryParam
    : "all";

  useEffect(() => {
    let isCurrentRequest = true;

    async function loadProducts() {
      try {
        setIsLoading(true);
        setHasError(false);
        const result = await getProductPage(
          {
            page: currentPage,
            pageSize: PRODUCTS_PER_PAGE,
            category: selectedCategory,
            sort: sortOrder,
            availableOnly,
            deliveryOnly,
          },
        );

        if (!isCurrentRequest) return;

        setProducts(result.items);
        setTotalCount(result.totalCount);
        setTotalPages(result.totalPages);
      } catch (error) {
        if (!isCurrentRequest) return;

        console.error(error);
        setHasError(true);
      } finally {
        if (isCurrentRequest) {
          setIsLoading(false);
        }
      }
    }

    void loadProducts();

    return () => {
      isCurrentRequest = false;
    };
  }, [selectedCategory, sortOrder, availableOnly, deliveryOnly, currentPage]);

  function resetToFirstPage() {
    setCurrentPage(1);
  }

  function handleCategoryChange(value: ProductCategoryFilter) {
    const nextParams = new URLSearchParams(searchParams);

    if (value === "all") {
      nextParams.delete("category");
    } else {
      nextParams.set("category", value);
    }

    resetToFirstPage();
    setSearchParams(nextParams);
  }

  function handleSortChange(value: ProductSortOrder) {
    resetToFirstPage();
    setSortOrder(value);
  }

  function handleAvailableOnlyChange(value: boolean) {
    resetToFirstPage();
    setAvailableOnly(value);
  }

  function handleDeliveryOnlyChange(value: boolean) {
    resetToFirstPage();
    setDeliveryOnly(value);
  }

  function handlePageChange(page: number) {
    setCurrentPage(page);
    requestAnimationFrame(() => {
      document.getElementById("products-top")?.scrollIntoView({
        behavior: "smooth",
        block: "start",
      });
    });
  }

  const firstVisibleItem = totalCount === 0
    ? 0
    : (currentPage - 1) * PRODUCTS_PER_PAGE + 1;
  const lastVisibleItem = Math.min(
    currentPage * PRODUCTS_PER_PAGE,
    totalCount,
  );

  return (
    <div className="mx-auto max-w-6xl px-4 py-4">
      <Seo
        title="Termékek"
        description="Válogass a Bakony Kincse Biokertészet természetes termékei között: gyógynövények, teák, fűszerek, illóolajok és hidrolátumok."
      />
      <ProductNavigation />

      <section
        id="products-top"
        className="mt-4 scroll-mt-20"
      >
        <div className="mb-5">
          <h1 className="text-2xl font-bold text-site-heading sm:text-3xl">
            Termékeink
          </h1>

          {!isLoading && !hasError && totalCount > 0 && (
            <p className="mt-2 text-sm text-site-text">
              {firstVisibleItem}-{lastVisibleItem} termék, összesen: {totalCount} db ·{" "}
              {currentPage} / {totalPages}. oldal
            </p>
          )}
        </div>

        {!hasError && (
          <ProductControls
            category={selectedCategory}
            sortOrder={sortOrder}
            availableOnly={availableOnly}
            deliveryOnly={deliveryOnly}
            onCategoryChange={handleCategoryChange}
            onSortChange={handleSortChange}
            onAvailableOnlyChange={handleAvailableOnlyChange}
            onDeliveryOnlyChange={handleDeliveryOnlyChange}
          />
        )}

        {isLoading && (
          <div className="rounded-sm bg-site-surface-soft p-8 text-center text-site-text">
            Termékek betöltése...
          </div>
        )}

        {hasError && (
          <div className="rounded-sm bg-site-danger-soft p-8 text-center text-site-danger">
            Hiba történt a termékek betöltése közben.
          </div>
        )}

        {!isLoading && !hasError && totalCount === 0 && (
          <div className="rounded-sm bg-site-surface-soft p-8 text-center text-site-text">
            Nem található termék a megadott feltételekkel.
          </div>
        )}

        {!isLoading && !hasError && products.length > 0 && (
          <>
            <ProductCards products={products} />
            <ProductPagination
              currentPage={currentPage}
              totalPages={totalPages}
              onPageChange={handlePageChange}
            />
          </>
        )}
      </section>
    </div>
  );
}
