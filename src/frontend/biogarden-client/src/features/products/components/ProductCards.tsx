import { managedImage } from "../../../shared/utils/managedImage";
import { useRef, useState } from "react";
import { Link } from "react-router";
import { useCart } from "../../cart/context/useCart";
import { ROUTES } from "../../../shared/constants/routes";
import { getErrorMessage } from "../../../shared/utils/errorHandling";
import type { ProductListItem } from "../types/product.types";
import {
  formatProductPrice,
  getProductCategoryLabel,
  getProductPackageLabel,
} from "../utils/productLabels";


type ProductCardsProps = {
  products: ProductListItem[];
};

export default function ProductCards({ products }: ProductCardsProps) {
  const { addItem } = useCart();
  const pendingProducts = useRef(new Set<number>());
  const [addingProductIds, setAddingProductIds] = useState<ReadonlySet<number>>(new Set());
  const [errors, setErrors] = useState<Record<number, string | undefined>>({});

  async function handleAddToCart(productId: number) {
    if (pendingProducts.current.has(productId)) return;
    pendingProducts.current.add(productId);
    setAddingProductIds(new Set(pendingProducts.current));
    setErrors(current => ({ ...current, [productId]: undefined }));
    try {
      await addItem(productId);
    } catch (error) {
      setErrors(current => ({ ...current, [productId]: getErrorMessage(error, "A terméket nem sikerült kosárba tenni.") }));
    } finally {
      pendingProducts.current.delete(productId);
      setAddingProductIds(new Set(pendingProducts.current));
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
                {...managedImage(product.image)}
                sizes="(min-width: 1280px) 280px, (min-width: 1024px) 33vw, (min-width: 640px) 50vw, 100vw"
                alt={product.name}
                className="aspect-square w-full rounded-sm object-cover transition duration-300 group-hover:scale-[1.02]"
                width={600}
                height={600}
                loading="lazy"
                decoding="async"
              />

              {(!product.available || !product.inStock) && (
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
                  disabled={(!product.available || !product.inStock) || addingProductIds.has(product.id)}
                  className="inline-flex rounded-sm border border-transparent bg-site-green px-4 py-2 text-sm font-semibold text-white transition-colors hover:bg-site-green-hover disabled:cursor-not-allowed disabled:border-site-border disabled:bg-transparent disabled:text-site-muted disabled:hover:bg-transparent"
                >
                  {addingProductIds.has(product.id) ? "Hozzáadás..." : "Kosárba"}
                </button>
              </div>
              {errors[product.id] && <p role="alert" className="mt-3 text-sm font-semibold text-site-danger">{errors[product.id]}</p>}
            </div>
          </article>
        );
      })}
    </div>
  );
}
