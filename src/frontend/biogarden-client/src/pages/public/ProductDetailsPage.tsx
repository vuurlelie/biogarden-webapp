import { useEffect, useRef, useState } from "react";
import { Link, useParams } from "react-router";
import { getProductById, getProducts } from "../../features/products/api/productsApi";
import ProductCards from "../../features/products/components/ProductCards";
import type {
  ProductCategory,
  ProductDetails,
  ProductListItem,
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
import { getAvailableQuantityToAdd } from "../../features/cart/api/cartApi";
import Seo from "../../shared/seo/Seo";
import { useModalFocus } from "../../shared/hooks/useModalFocus";
import { useAnalytics } from "../../features/analytics/context/AnalyticsContextCore";
import { getErrorMessage } from "../../shared/utils/errorHandling";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;
const SITE_URL = (import.meta.env.VITE_SITE_URL ?? "https://bakonykincse.hu").replace(/\/$/, "");

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
  const [relatedProducts, setRelatedProducts] = useState<ProductListItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [hasError, setHasError] = useState(false);
  const [notFound, setNotFound] = useState(false);
  const [isAddingToCart, setIsAddingToCart] = useState(false);
  const [cartMessage, setCartMessage] = useState<string | null>(null);
  const [quantity, setQuantity] = useState(1);
  const [availableToAdd, setAvailableToAdd] = useState(0);
  const [isImageOpen, setIsImageOpen] = useState(false);
  const imageModalRef = useRef<HTMLDivElement | null>(null);
  const { track } = useAnalytics();

  useModalFocus(imageModalRef, {
    isOpen: isImageOpen,
    onClose: () => setIsImageOpen(false),
  });
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
          setRelatedProducts([]);
          return;
        }

        setProduct(loadedProduct);
        const products = await getProducts();
        if (shouldIgnoreResult) return;

        const otherProducts = products.filter((item) => item.id !== loadedProduct.id);
        const sameCategoryProducts = otherProducts.filter((item) => item.category === loadedProduct.category);
        const fallbackProducts = otherProducts.filter((item) => item.category !== loadedProduct.category);
        setRelatedProducts([...sameCategoryProducts, ...fallbackProducts].slice(0, 4));

        const availableQuantity = await getAvailableQuantityToAdd(loadedProduct.id);
        if (shouldIgnoreResult) return;
        setAvailableToAdd(availableQuantity);
        setQuantity(availableQuantity > 0 ? 1 : 0);
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

  useEffect(() => {
    if (!product) return;
    track("product_view", {
      productId: product.id,
      category: String(product.category),
    });
  }, [product, track]);

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
  const lowStock = product.available && availableToAdd > 0 && availableToAdd <= 10;
  const productStructuredData = {
    "@context": "https://schema.org",
    "@type": "Product",
    name: product.name,
    description:
      product.description ??
      `${product.name} a Bakony Kincse Biokertészet természetes termékei között.`,
    image: getProductImageUrl(product.image),
    sku: String(product.id),
    brand: {
      "@type": "Brand",
      name: "Bakony Kincse Biokertészet",
    },
    offers: {
      "@type": "Offer",
      url: `${SITE_URL}${ROUTES.products}/${product.id}`,
      priceCurrency: "HUF",
      price: product.price,
      availability: product.available ? "https://schema.org/InStock" : "https://schema.org/OutOfStock",
    },
  };

  async function handleAddToCart() {
    const currentProduct = product;
    if (!currentProduct) return;

    setIsAddingToCart(true);
    setCartMessage(null);
    try {
      await addItem(currentProduct.id, quantity);
      const remaining = await getAvailableQuantityToAdd(currentProduct.id);
      setAvailableToAdd(remaining);
      setQuantity(remaining > 0 ? 1 : 0);
      setCartMessage(`${quantity} ${getUnitLabel(currentProduct.unit)} bekerült a kosárba.`);
    } catch (error) {
      setCartMessage(getErrorMessage(error, "A terméket nem sikerült kosárba tenni."));
    } finally {
      setIsAddingToCart(false);
    }
  }

  return (
    <main className="mx-auto max-w-6xl px-4 py-4 sm:py-6">
      <Seo
        title={product.name}
        description={
          product.description?.slice(0, 155) ??
          `${product.name} a Bakony Kincse Biokertészet természetes termékei között.`
        }
        image={getProductImageUrl(product.image)}
        type="product"
        structuredData={productStructuredData}
      />
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

      <article className="mt-8 grid gap-8 lg:grid-cols-2 lg:items-start">
        <div className="min-w-0 self-start">
          <button
            type="button"
            onClick={() => setIsImageOpen(true)}
            className="block w-full max-w-[420px] cursor-zoom-in rounded-lg border border-gray-200 bg-white p-2 text-left shadow-lg focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-site-green"
            aria-label="Termékkép nagyítása"
          >
            <img
              src={getProductImageUrl(product.image)}
              alt={product.name}
              className="aspect-square max-h-[420px] w-full max-w-[420px] cursor-zoom-in rounded-sm object-cover"
            />
          </button>
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
              <p className="mt-5 whitespace-pre-line text-justify text-base italic leading-7 text-site-text">
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
                Ebből a termékből még {availableToAdd}{" "}
                {getUnitLabel(product.unit)} áll rendelkezésre.
              </p>
            )}

            <div className="mt-7 flex flex-wrap items-center gap-3">
              <label className="flex items-center gap-2 text-sm font-semibold text-site-heading">
                Mennyiség
                <input
                  type="number"
                  min={availableToAdd > 0 ? 1 : 0}
                  max={availableToAdd}
                  value={quantity}
                  onChange={(event) => {
                    const next = Number(event.target.value);
                    setQuantity(Math.min(availableToAdd, Math.max(1, Number.isFinite(next) ? next : 1)));
                  }}
                  disabled={availableToAdd === 0 || isAddingToCart}
                  className="w-20 rounded-sm border border-site-border px-3 py-2 text-center"
                />
                <span className="text-site-muted">{getUnitLabel(product.unit)}</span>
              </label>
              <button
                type="button"
                onClick={() => void handleAddToCart()}
                disabled={!product.available || availableToAdd === 0 || quantity < 1 || isAddingToCart}
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
            {product.available && availableToAdd === 0 && <p className="mt-3 text-sm font-semibold text-site-danger">A teljes készlet jelenleg más kosarakban van lefoglalva vagy már a kosaradban található.</p>}
            {cartMessage && <p className="mt-3 text-sm font-semibold text-site-green-dark" role="status">{cartMessage}</p>}
        </div>
      </article>

      {relatedProducts.length > 0 && (
        <section className="mt-14 border-t border-site-border pt-10" aria-labelledby="related-products-title">
          <h2 id="related-products-title" className="text-2xl font-bold text-site-heading">
            Kapcsolódó termékek
          </h2>
          <div className="mt-5">
            <ProductCards products={relatedProducts} />
          </div>
        </section>
      )}

      {isImageOpen && (
        <div
          className="fixed inset-0 z-[200] flex items-center justify-center bg-site-heading/80 p-4 pt-28 backdrop-blur-sm sm:p-6 sm:pt-32"
          role="dialog"
          aria-modal="true"
          aria-label="Termékkép nagy nézetben"
          onClick={() => setIsImageOpen(false)}
        >
          <div
            ref={imageModalRef}
            tabIndex={-1}
            className="relative inline-flex max-h-[calc(100vh-9rem)] max-w-[94vw] rounded-sm border border-white/35 bg-white p-2 shadow-2xl sm:max-h-[calc(100vh-10rem)]"
            onClick={(event) => event.stopPropagation()}
          >
            <button
              type="button"
              onClick={() => setIsImageOpen(false)}
              className="absolute right-3 top-3 z-10 grid h-10 w-10 place-items-center rounded-full bg-white/95 text-2xl font-bold leading-none text-site-heading shadow-md transition hover:bg-site-surface-soft focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-site-green"
              aria-label="Kép bezárása"
            >
              ×
            </button>
            <img
              src={getProductImageUrl(product.image)}
              alt={product.name}
              className="max-h-[calc(100vh-10rem)] max-w-[calc(94vw-1rem)] rounded-sm object-contain sm:max-h-[calc(100vh-11rem)]"
            />
          </div>
        </div>
      )}
    </main>
  );
}
