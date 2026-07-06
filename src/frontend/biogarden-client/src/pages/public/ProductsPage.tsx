import { useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { getProducts } from "../../features/products/api/productsApi";
import ProductCards from "../../features/products/components/ProductCards";
import ProductControls from "../../features/products/components/ProductControls";
import ProductNavigation from "../../features/products/components/ProductNavigation";
import ProductPagination from "../../features/products/components/ProductPagination";
import type {
  ProductCategoryFilter,
  ProductListItem,
  ProductSortOrder,
} from "../../features/products/types/product.types";
import {
  isProductCategoryFilter,
  matchesProductCategory,
} from "../../features/products/utils/productFilters";

const PRODUCTS_PER_PAGE = 12;

export default function ProductsPage() {
  const [products, setProducts] = useState<ProductListItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [hasError, setHasError] = useState(false);
  const [sortOrder, setSortOrder] = useState<ProductSortOrder>("abc");
  const [availableOnly, setAvailableOnly] = useState(false);
  const [deliveryOnly, setDeliveryOnly] = useState(false);
  const [currentPage, setCurrentPage] = useState(1);
  const [searchParams, setSearchParams] = useSearchParams();

  const categoryParam = searchParams.get("category");
  const selectedCategory: ProductCategoryFilter = isProductCategoryFilter(
    categoryParam,
  )
    ? categoryParam
    : "all";

  useEffect(() => {
    let shouldIgnoreResult = false;

    async function loadProducts() {
      try {
        setIsLoading(true);
        setHasError(false);
        const loadedProducts = await getProducts();

        if (!shouldIgnoreResult) {
          setProducts(loadedProducts);
        }
      } catch (error) {
        if (shouldIgnoreResult) return;

        console.error(error);
        setHasError(true);
      } finally {
        if (!shouldIgnoreResult) {
          setIsLoading(false);
        }
      }
    }

    void loadProducts();

    return () => {
      shouldIgnoreResult = true;
    };
  }, []);

  const filteredProducts = useMemo(() => {
    return products.filter((product) => {
      if (!matchesProductCategory(product.category, selectedCategory)) {
        return false;
      }

      if (availableOnly && !product.available) {
        return false;
      }

      return !deliveryOnly || product.deliveryAvailable;
    });
  }, [products, selectedCategory, availableOnly, deliveryOnly]);

  const sortedProducts = useMemo(() => {
    return [...filteredProducts].sort((first, second) => {
      if (sortOrder === "price-asc") return first.price - second.price;
      if (sortOrder === "price-desc") return second.price - first.price;

      return first.name.localeCompare(second.name, "hu", {
        sensitivity: "base",
      });
    });
  }, [filteredProducts, sortOrder]);

  const totalPages = Math.max(
    1,
    Math.ceil(sortedProducts.length / PRODUCTS_PER_PAGE),
  );
  const startIndex = (currentPage - 1) * PRODUCTS_PER_PAGE;
  const paginatedProducts = sortedProducts.slice(
    startIndex,
    startIndex + PRODUCTS_PER_PAGE,
  );

  useEffect(() => {
    setCurrentPage(1);
  }, [selectedCategory, sortOrder, availableOnly, deliveryOnly]);

  function handleCategoryChange(value: ProductCategoryFilter) {
    const nextParams = new URLSearchParams(searchParams);

    if (value === "all") {
      nextParams.delete("category");
    } else {
      nextParams.set("category", value);
    }

    setSearchParams(nextParams);
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

  return (
    <div className="mx-auto max-w-6xl px-4 py-4">
      <ProductNavigation />

      <section
        id="products-top"
        className="mt-4 scroll-mt-20 rounded-sm bg-white p-4 shadow-lg sm:p-6"
      >
        <div className="mb-5">
          <div>
            <h1 className="text-2xl font-bold text-site-heading sm:text-3xl">
              Termékeink
            </h1>
          </div>

          {!isLoading && !hasError && sortedProducts.length > 0 && (
            <p className="mt-2 text-sm text-site-text">
              {startIndex + 1}–
              {Math.min(
                startIndex + PRODUCTS_PER_PAGE,
                sortedProducts.length,
              )}{" "}
              termék, összesen: {sortedProducts.length} db · {currentPage} /{" "}
              {totalPages}. oldal
            </p>
          )}
        </div>

        {!isLoading && !hasError && products.length > 0 && (
          <ProductControls
            category={selectedCategory}
            sortOrder={sortOrder}
            availableOnly={availableOnly}
            deliveryOnly={deliveryOnly}
            onCategoryChange={handleCategoryChange}
            onSortChange={setSortOrder}
            onAvailableOnlyChange={setAvailableOnly}
            onDeliveryOnlyChange={setDeliveryOnly}
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

        {!isLoading && !hasError && products.length === 0 && (
          <div className="rounded-sm bg-site-surface-soft p-8 text-center text-site-text">
            Jelenleg nincs megjeleníthető termék.
          </div>
        )}

        {!isLoading && !hasError && products.length > 0 && sortedProducts.length === 0 && (
          <div className="rounded-sm bg-site-surface-soft p-8 text-center text-site-text">
            Nem található termék a megadott feltételekkel.
          </div>
        )}

        {!isLoading && !hasError && paginatedProducts.length > 0 && (
          <>
            <ProductCards products={paginatedProducts} />
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
