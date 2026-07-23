import { lazy, Suspense, useCallback, useEffect, useState } from "react";
import { deleteAdminProduct, getAdminProductPage } from "../../features/admin/api/adminProductsApi";
import type { ProductCategory, ProductDetails } from "../../features/products/types/product.types";
import {
  formatProductPrice,
  getProductCategoryLabel,
  getUnitLabel,
} from "../../features/products/utils/productLabels";
import AdminPageHeader from "../../shared/components/admin/AdminPageHeader";
import AdminPagination from "../../shared/components/admin/AdminPagination";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;
const PAGE_SIZE = 12;

const AdminProductFormModal = lazy(() => import("../../features/admin/components/AdminProductFormModal"));

type AvailabilityFilter = "all" | "available" | "unavailable";

function imageUrl(path: string): string {
  return `${API_BASE_URL}/${path.replace(/^\/+/, "")}`;
}

export default function AdminProductsPage() {
  const [products, setProducts] = useState<ProductDetails[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [category, setCategory] = useState<"all" | ProductCategory>("all");
  const [availability, setAvailability] = useState<AvailabilityFilter>("all");
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(1);
  const [editingProduct, setEditingProduct] = useState<ProductDetails | null | undefined>(undefined);
  const [deletingProduct, setDeletingProduct] = useState<ProductDetails | null>(null);
  const [deleting, setDeleting] = useState(false);

  const loadProducts = useCallback(async (signal?: AbortSignal) => {
    setLoading(true);
    setError(null);

    try {
      const result = await getAdminProductPage(
        {
          page,
          pageSize: PAGE_SIZE,
          search,
          category,
          availability,
        },
        signal,
      );
      setProducts(result.items);
      setTotalCount(result.totalCount);
      setTotalPages(result.totalPages);
    } catch (loadError) {
      if (!(loadError instanceof DOMException && loadError.name === "AbortError")) {
        setError(loadError instanceof Error ? loadError.message : "A termékek betöltése nem sikerült.");
      }
    } finally {
      if (!signal?.aborted) setLoading(false);
    }
  }, [availability, category, page, search]);

  useEffect(() => {
    const controller = new AbortController();
    void loadProducts(controller.signal);
    return () => controller.abort();
  }, [loadProducts]);

  useEffect(() => {
    if (!message) return;
    const timeout = window.setTimeout(() => setMessage(null), 4000);
    return () => window.clearTimeout(timeout);
  }, [message]);

  useEffect(() => {
    if (page > totalPages) {
      setPage(totalPages);
    }
  }, [page, totalPages]);

  async function confirmDelete() {
    if (!deletingProduct) return;

    setDeleting(true);
    setError(null);

    try {
      await deleteAdminProduct(deletingProduct.id);
      setDeletingProduct(null);
      setMessage("A termék törlése sikerült.");
      await loadProducts();
    } catch (deleteError) {
      setError(deleteError instanceof Error ? deleteError.message : "A törlés nem sikerült.");
      setDeletingProduct(null);
    } finally {
      setDeleting(false);
    }
  }

  async function handleSaved(successMessage: string) {
    setEditingProduct(undefined);
    setMessage(successMessage);
    await loadProducts();
  }

  function resetPage() {
    setPage(1);
  }

  return (
    <div className="space-y-6">
      <AdminPageHeader
        title="Termékek"
        description="A webáruház termékeinek létrehozása, szerkesztése és elérhetőségének kezelése."
        actions={(
          <button
            type="button"
            onClick={() => setEditingProduct(null)}
            className="rounded-sm bg-site-green px-4 py-3 text-sm font-semibold text-white hover:bg-site-green-hover"
          >
            + Új termék
          </button>
        )}
      />

      {message && (
        <div role="status" className="rounded-sm border border-site-green/25 bg-site-surface-soft p-4 font-semibold text-site-green-dark">
          {message}
        </div>
      )}

      {error && (
        <div role="alert" className="flex flex-wrap items-center justify-between gap-3 rounded-sm bg-site-danger-soft p-4 text-site-danger">
          <span>{error}</span>
          <button
            type="button"
            onClick={() => void loadProducts()}
            className="rounded-sm border border-site-danger px-3 py-2 text-sm font-semibold"
          >
            Újrapróbálás
          </button>
        </div>
      )}

      <section
        className="rounded-sm border border-site-border bg-white p-4 shadow-sm sm:p-5"
        aria-label="Termékek szűrése"
      >
        <div className="grid gap-4 md:grid-cols-3">
          <label className="text-sm font-semibold">
            Keresés
            <input
              value={search}
              onChange={(event) => {
                setSearch(event.target.value);
                resetPage();
              }}
              placeholder="Termék neve"
              className="mt-1.5 w-full rounded-sm border border-site-border px-3 py-2.5 outline-none focus:border-site-green"
            />
          </label>

          <label className="text-sm font-semibold">
            Kategória
            <select
              value={category}
              onChange={(event) => {
                setCategory(event.target.value === "all" ? "all" : Number(event.target.value) as ProductCategory);
                resetPage();
              }}
              className="mt-1.5 w-full rounded-sm border border-site-border px-3 py-2.5"
            >
              <option value="all">Minden kategória</option>
              <option value="0">Zöldség</option>
              <option value="2">Gyógytea</option>
              <option value="3">Illóolaj</option>
              <option value="4">Hidrolátum</option>
              <option value="5">Fűszer</option>
            </select>
          </label>

          <label className="text-sm font-semibold">
            Elérhetőség
            <select
              value={availability}
              onChange={(event) => {
                setAvailability(event.target.value as AvailabilityFilter);
                resetPage();
              }}
              className="mt-1.5 w-full rounded-sm border border-site-border px-3 py-2.5"
            >
              <option value="all">Mindegyik</option>
              <option value="available">Elérhető</option>
              <option value="unavailable">Nem elérhető</option>
            </select>
          </label>
        </div>
        <p className="mt-4 text-sm text-site-muted">
          {totalCount} termék
        </p>
      </section>

      {loading ? (
        <div className="grid gap-5 md:grid-cols-2 xl:grid-cols-3">
          {Array.from({ length: 6 }, (_, index) => (
            <div key={index} className="h-80 animate-pulse rounded-sm border border-site-border bg-white" />
          ))}
        </div>
      ) : totalCount === 0 ? (
        <section className="rounded-sm border border-site-border bg-white p-8 text-center shadow-sm">
          <h2 className="text-lg font-bold text-site-heading">Nincs találat</h2>
          <p className="mt-2 text-sm text-site-muted">Módosítsd a keresési vagy szűrési feltételeket.</p>
        </section>
      ) : (
        <section className="grid gap-5 md:grid-cols-2 xl:grid-cols-3" aria-label="Terméklista">
          {products.map((product) => (
            <article key={product.id} className="flex flex-col overflow-hidden rounded-sm border border-site-border bg-white shadow-sm">
              <div className="relative aspect-[16/9] bg-site-surface-soft">
                <img src={imageUrl(product.image)} alt="" className="h-full w-full object-cover" />
                <span
                  className={`absolute left-3 top-3 rounded-full px-3 py-1 text-xs font-bold ${
                    product.available ? "bg-site-green text-white" : "bg-white text-site-muted shadow"
                  }`}
                >
                  {product.available ? "Elérhető" : "Nem elérhető"}
                </span>
              </div>

              <div className="flex flex-1 flex-col p-5">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <p className="text-xs font-bold uppercase tracking-wider text-site-green-dark">
                      {getProductCategoryLabel(product.category)}
                    </p>
                    <h2 className="mt-1 text-xl font-bold text-site-heading">{product.name}</h2>
                  </div>
                  <span
                    className={`shrink-0 rounded-full px-2.5 py-1 text-xs font-bold ${
                      product.stockQuantity <= 5
                        ? "bg-site-danger-soft text-site-danger"
                        : "bg-site-surface-soft text-site-green-dark"
                    }`}
                  >
                    {product.stockQuantity} {getUnitLabel(product.unit)}
                  </span>
                </div>

                <p className="mt-3 line-clamp-3 text-sm leading-6 text-site-muted">
                  {product.description || "Nincs megadott leírás."}
                </p>

                <dl className="mt-4 grid grid-cols-2 gap-3 border-t border-site-border pt-4 text-sm">
                  <div>
                    <dt className="text-xs text-site-muted">Ár</dt>
                    <dd className="mt-1 font-bold text-site-heading">{formatProductPrice(product)}</dd>
                  </div>
                  <div>
                    <dt className="text-xs text-site-muted">Szállítás</dt>
                    <dd className="mt-1 font-semibold text-site-text">
                      {product.deliveryAvailable ? "Kiszállítható" : "Csak átvétel"}
                    </dd>
                  </div>
                </dl>

                <div className="mt-auto flex gap-3 pt-5">
                  <button
                    type="button"
                    onClick={() => setEditingProduct(product)}
                    className="flex-1 rounded-sm bg-site-green px-4 py-2.5 font-semibold text-white hover:bg-site-green-hover"
                  >
                    Szerkesztés
                  </button>
                  <button
                    type="button"
                    onClick={() => setDeletingProduct(product)}
                    className="rounded-sm border border-site-danger/40 px-4 py-2.5 font-semibold text-site-danger hover:bg-site-danger-soft"
                  >
                    Törlés
                  </button>
                </div>
              </div>
            </article>
          ))}
        </section>
      )}

      {!loading && totalCount > 0 && (
        <AdminPagination page={page} totalPages={totalPages} onPageChange={setPage} />
      )}

      {editingProduct !== undefined && (
        <Suspense
          fallback={(
            <div className="fixed inset-0 z-[80] grid place-items-center bg-black/55 p-4 text-center font-semibold text-white">
              Szerkesztő betöltése...
            </div>
          )}
        >
          <AdminProductFormModal
            product={editingProduct}
            onClose={() => setEditingProduct(undefined)}
            onSaved={(text) => void handleSaved(text)}
          />
        </Suspense>
      )}

      {deletingProduct && (
        <div
          className="fixed inset-0 z-[90] grid place-items-center bg-black/55 p-4"
          role="dialog"
          aria-modal="true"
          aria-labelledby="delete-product-title"
        >
          <section className="w-full max-w-md rounded-sm bg-white p-6 shadow-xl">
            <h2 id="delete-product-title" className="text-xl font-bold text-site-heading">
              Termék törlése
            </h2>
            <p className="mt-3 leading-6 text-site-muted">
              Biztosan törölni szeretnéd ezt a terméket:{" "}
              <strong className="text-site-heading">{deletingProduct.name}</strong>?
            </p>
            <p className="mt-2 text-sm text-site-danger">A termék többé nem jelenik meg a vásárlóknak.</p>
            <div className="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
              <button
                type="button"
                onClick={() => setDeletingProduct(null)}
                className="rounded-sm border border-site-border px-4 py-3 font-semibold"
              >
                Mégse
              </button>
              <button
                type="button"
                disabled={deleting}
                onClick={() => void confirmDelete()}
                className="rounded-sm bg-site-danger px-4 py-3 font-semibold text-white disabled:opacity-60"
              >
                {deleting ? "Törlés..." : "Törlés"}
              </button>
            </div>
          </section>
        </div>
      )}
    </div>
  );
}
