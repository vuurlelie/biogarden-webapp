import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { getProductById } from "../../features/products/api/productsApi";
import type {
  ProductCategory,
  ProductDetails,
} from "../../features/products/types/product.types";
import {
  formatProductPrice,
  getProductCategoryLabel,
  getProductPackageLabel,
  getUnitLabel,
} from "../../features/products/utils/productLabels";
import { ROUTES } from "../../shared/constants/routes";
import NotFoundPage from "./NotFoundPage";
import { useCart } from "../../features/cart/context/useCart";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

const CATEGORY_LINKS: Record<ProductCategory, { label: string; value: string }> = {
  0: { label: "Zöldségek", value: "zoldsegek" },
  2: { label: "Fűszerek, gyógyteák", value: "fuszerek-gyogyteak" },
  3: {
    label: "Illóolajok, hidrolátumok",
    value: "illoolajok-hidrolatumok",
  },
  4: {
    label: "Illóolajok, hidrolátumok",
    value: "illoolajok-hidrolatumok",
  },
  5: { label: "Fűszerek, gyógyteák", value: "fuszerek-gyogyteak" },
};

function getProductImageUrl(image: string): string {
  return `${API_BASE_URL}/${image.replace(/^\/+/, "")}`;
}

export default function ProductDetailsPage() {
  const { productId } = useParams<{ productId: string }>();
  const [product, setProduct] = useState<ProductDetails | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [hasError, setHasError] = useState(false);
  const [notFound, setNotFound] = useState(false);
  const [isAddingToCart, setIsAddingToCart] = useState(false);
  const [cartMessage, setCartMessage] = useState<string | null>(null);
  const { addItem } = useCart();

  useEffect(() => {
    const parsedProductId = Number(productId);
    let shouldIgnoreResult = false;

    if (!Number.isInteger(parsedProductId) || parsedProductId <= 0) {
      setNotFound(true);
      setIsLoading(false);
      return;
    }

    async function loadProduct() {
      try {
        setIsLoading(true);
        setHasError(false);
        setNotFound(false);

        const loadedProduct = await getProductById(parsedProductId);

        if (shouldIgnoreResult) return;

        if (loadedProduct === null) {
          setNotFound(true);
          return;
        }

        setProduct(loadedProduct);
      } catch (error) {
        if (shouldIgnoreResult) return;
        console.error(error);
        setHasError(true);
      } finally {
        if (!shouldIgnoreResult) setIsLoading(false);
      }
    }

    void loadProduct();
    return () => {
      shouldIgnoreResult = true;
    };
  }, [productId]);

  if (isLoading) {
    return (
      <div className="mx-auto max-w-6xl px-4 py-12">
        <div className="rounded-sm bg-site-surface-soft p-10 text-center text-site-text shadow-lg">
          Termék betöltése...
        </div>
      </div>
    );
  }

  if (notFound) {
    return (
      <NotFoundPage message="A keresett azonosítójú termék sajnos nem található." />
    );
  }

  if (hasError || !product) {
    return (
      <div className="mx-auto max-w-6xl px-4 py-12">
        <div className="rounded-sm bg-site-danger-soft p-10 text-center text-site-danger shadow-lg">
          Hiba történt a termék betöltése közben.
        </div>
      </div>
    );
  }

  const categoryLink = CATEGORY_LINKS[product.category];
  const categoryUrl = `${ROUTES.products}?category=${categoryLink.value}`;
  const packageLabel = getProductPackageLabel(product);
  const lowStock = product.available && product.stockQuantity <= 10;

  async function handleAddToCart() {
    setIsAddingToCart(true);
    setCartMessage(null);
    try {
      await addItem(product!.id);
      setCartMessage("A termék bekerült a kosárba.");
    } catch (error) {
      setCartMessage(error instanceof Error ? error.message : "A terméket nem sikerült kosárba tenni.");
    } finally {
      setIsAddingToCart(false);
    }
  }

  return (
    <main className="mx-auto max-w-6xl px-4 py-4 sm:py-6">
      <nav
        className="flex flex-wrap items-center gap-2 text-sm text-site-text"
        aria-label="Morzsamenü"
      >
        <Link to={ROUTES.home} className="font-bold text-site-green-dark hover:underline">
          Kezdőlap
        </Link>
        <span aria-hidden="true">/</span>
        <Link to={ROUTES.products} className="font-bold text-site-green-dark hover:underline">
          Termékeink
        </Link>
        <span aria-hidden="true">/</span>
        <Link to={categoryUrl} className="font-bold text-site-green-dark hover:underline">
          {categoryLink.label}
        </Link>
        <span aria-hidden="true">/</span>
        <span className="min-w-0 truncate" aria-current="page">
          {product.name}
        </span>
      </nav>

      <article className="mt-4 overflow-hidden rounded-sm border border-site-border bg-white shadow-lg">
        <div className="grid gap-8 p-4 sm:p-6 lg:grid-cols-[minmax(0,0.95fr)_minmax(0,1.05fr)] lg:gap-12 lg:p-10">
          <div className="self-start rounded-sm bg-site-surface p-3">
            <img
              src={getProductImageUrl(product.image)}
              alt={product.name}
              className="aspect-square w-full rounded-sm object-cover"
            />
          </div>

          <div className="flex min-w-0 flex-col py-1">
            <Link
              to={categoryUrl}
              className="w-fit text-xs font-bold uppercase tracking-[0.16em] text-site-green-dark hover:underline"
            >
              {getProductCategoryLabel(product.category)}
            </Link>
            <h1 className="mt-2 text-3xl font-bold leading-tight text-site-heading sm:text-4xl">
              {product.name}
            </h1>

            <div className="mt-5 flex flex-wrap items-baseline gap-x-5 gap-y-2 border-y border-site-border py-4">
              <span className="text-2xl font-bold text-site-heading">
                {formatProductPrice(product)}
              </span>
              {packageLabel && (
                <span className="text-sm font-semibold text-site-text">
                  Kiszerelés: {packageLabel}
                </span>
              )}
            </div>

            {product.description && (
              <p className="mt-5 whitespace-pre-line text-justify leading-7 text-site-text">
                {product.description}
              </p>
            )}

            <dl className="mt-6 grid gap-3 rounded-sm bg-site-surface-soft p-4 text-sm sm:grid-cols-2">
              <div>
                <dt className="font-bold text-site-heading">Elérhetőség</dt>
                <dd className={product.available ? "text-site-green-dark" : "text-site-danger"}>
                  {product.available ? "Jelenleg elérhető" : "Jelenleg nem elérhető"}
                </dd>
              </div>
              <div>
                <dt className="font-bold text-site-heading">Átvétel</dt>
                <dd>
                  {product.deliveryAvailable
                    ? "Házhozszállítással is kérhető"
                    : "Személyes átvétellel"}
                </dd>
              </div>
            </dl>

            {lowStock && (
              <p className="mt-4 text-sm font-semibold text-site-danger">
                Ebből a termékből már csak {product.stockQuantity}{" "}
                {getUnitLabel(product.unit)} áll rendelkezésre.
              </p>
            )}

            <p className="mt-6 text-sm italic leading-6 text-site-muted">
              Termékeink személyes átvétellel rendelhetők meg. A házhozszállításra
              alkalmas termékekről és az átvétel részleteiről a Kapcsolat résznél
              megadott elérhetőségeinken lehet egyeztetni.
            </p>

            <div className="mt-7 flex flex-wrap items-center gap-3">
              <button
                type="button"
                onClick={() => void handleAddToCart()}
                disabled={!product.available || isAddingToCart}
                className="inline-flex rounded-sm bg-site-green px-5 py-2.5 text-sm font-semibold text-white transition-colors hover:bg-site-green-hover disabled:cursor-not-allowed disabled:opacity-50"
              >
                {isAddingToCart ? "Kosárba helyezés..." : "Kosárba teszem"}
              </button>
              <Link
                to={ROUTES.products}
                className="inline-flex items-center gap-2 rounded-sm border border-site-green-dark px-5 py-2.5 text-sm font-semibold text-site-green-dark transition-colors hover:bg-site-green-dark hover:text-white"
              >
                <span aria-hidden="true">←</span>
                Vissza a termékekhez
              </Link>
            </div>
            {cartMessage && <p className="mt-3 text-sm font-semibold text-site-green-dark" role="status">{cartMessage}</p>}
          </div>
        </div>
      </article>
    </main>
  );
}
