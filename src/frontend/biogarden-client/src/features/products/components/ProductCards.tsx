import { useState } from "react";
import { Link } from "react-router";
import { useCart } from "../../cart/context/useCart";
import { ROUTES } from "../../../shared/constants/routes";
import type { ProductListItem } from "../types/product.types";
import {
  formatProductPrice,
  getProductCategoryLabel,
  getProductPackageLabel,
} from "../utils/productLabels";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

type ProductCardsProps = {
  products: ProductListItem[];
};

function getProductImageUrl(image: string): string {
  return `${API_BASE_URL}/${image.replace(/^\/+/, "")}`;
}

export default function ProductCards({ products }: ProductCardsProps) {
  const { addItem } = useCart();
  const [addingProductId, setAddingProductId] = useState<number | null>(null);

  async function handleAddToCart(productId: number) {
    setAddingProductId(productId);
    try {
      await addItem(productId);
    } catch {
      // Cart errors are presented on the cart page; the button becomes available again.
    } finally {
      setAddingProductId(null);
    }
  }

  return (
    <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
      {products.map((product) => {
        const detailsUrl = ROUTES.productDetails.replace(
          ":productId",
          String(product.id),
        );
        const packageLabel = getProductPackageLabel(product);

        return (
          <article
            key={product.id}
            className="flex min-w-0 flex-col overflow-hidden rounded-sm bg-white shadow-md transition hover:shadow-xl"
          >
            <Link to={detailsUrl} className="group relative block p-3">
              <img
                src={getProductImageUrl(product.image)}
                alt={product.name}
                className="h-64 w-full rounded-sm object-cover transition duration-300 group-hover:scale-[1.02]"
                width={600}
                height={600}
                loading="lazy"
                decoding="async"
              />

              {!product.available && (
                <span className="absolute bottom-6 left-1/2 -translate-x-1/2 whitespace-nowrap rounded-sm bg-white/95 px-4 py-2 text-xs font-bold uppercase tracking-wide text-site-text shadow-md">
                  Nem elérhető
                </span>
              )}
            </Link>

            <div className="flex flex-1 flex-col px-4 pb-4">
              <div className="text-xs font-bold uppercase tracking-wider text-site-green-dark">
                {getProductCategoryLabel(product.category)}
              </div>

              <div className="mt-2 min-w-0">
                <h2 className="truncate text-lg font-bold text-site-heading">
                  <Link
                    to={detailsUrl}
                    className="hover:text-site-green"
                    title={product.name}
                  >
                    {product.name}
                  </Link>
                </h2>
              </div>

              <div className="mt-3 flex items-baseline justify-between gap-3">
                <span className="font-semibold text-site-heading">
                  {formatProductPrice(product)}
                </span>
                {packageLabel && (
                  <span className="shrink-0 text-sm text-site-text">
                    {packageLabel}
                  </span>
                )}
              </div>

              <div className="mt-2 text-sm text-site-text">
                {product.deliveryAvailable
                  ? "Házhozszállítással elérhető"
                  : "Csak személyes átvétellel"}
              </div>

              <div className="mt-auto flex flex-wrap gap-2 pt-5">
                <Link
                  to={detailsUrl}
                  className="inline-flex rounded-sm border border-site-green-dark px-4 py-2 text-sm font-semibold text-site-green-dark transition-colors hover:bg-site-surface-soft"
                >
                  Részletek
                </Link>
                <button
                  type="button"
                  onClick={() => void handleAddToCart(product.id)}
                  disabled={!product.available || addingProductId === product.id}
                  className="inline-flex rounded-sm border border-transparent bg-site-green px-4 py-2 text-sm font-semibold text-white transition-colors hover:bg-site-green-hover disabled:cursor-not-allowed disabled:border-site-border disabled:bg-transparent disabled:text-site-muted disabled:hover:bg-transparent"
                >
                  {addingProductId === product.id ? "Hozzáadás..." : "Kosárba"}
                </button>
              </div>
            </div>
          </article>
        );
      })}
    </div>
  );
}
