import { useCallback, useEffect, useMemo, useState } from "react";
import {
  getAdminInventory,
  updateAdminInventory,
  type AdminInventoryItem,
} from "../../features/admin/api/adminInventoryApi";
import { getUnitLabel } from "../../features/products/utils/productLabels";
import AdminPageHeader from "../../shared/components/admin/AdminPageHeader";
import AdminPagination from "../../shared/components/admin/AdminPagination";
import { getErrorMessage, isAbortError, hasErrorStatus } from "../../shared/utils/errorHandling";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;
const PAGE_SIZE = 12;

type StockFilter = "all" | "low" | "empty" | "reserved";

function imageUrl(path: string) {
  return `${API_BASE_URL}/${path.replace(/^\/+/, "")}`;
}

function InventoryCard({
  item,
  onSaved,
}: {
  item: AdminInventoryItem;
  onSaved: (item: AdminInventoryItem) => void;
}) {
  const [editing, setEditing] = useState(false);
  const [quantity, setQuantity] = useState(String(item.stockQuantity));
  const [available, setAvailable] = useState(item.available);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [quantityError, setQuantityError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);
  const [previousInput, setPreviousInput] = useState("");
  async function reloadCurrent() {
    setSaving(true);
    try {
      const current = (await getAdminInventory()).find(value => value.productId === item.productId);
      if (!current) throw new Error("A termék már nem érhető el.");
      setPreviousInput(`Korábbi bevitel: ${quantity} db, ${available ? "rendelhető" : "nem rendelhető"}. Ellenőrizd az aktuális készletet, majd szükség esetén írd be újra a módosítást.`);
      onSaved(current);
      setQuantity(String(current.stockQuantity)); setAvailable(current.available);
      setConflict(false); setError(null); setQuantityError(null);
    } catch (error) { setError(getErrorMessage(error, "A frissítés nem sikerült.")); }
    finally { setSaving(false); }
  }
  const quantityErrorId = `inventory-${item.productId}-quantity-error`;

  async function save() {
    if (saving || conflict) return;
    const value = Number(quantity);

    if (!Number.isInteger(value) || value < item.reservedQuantity) {
      setQuantityError(`A készlet legalább a lefoglalt ${item.reservedQuantity} darab legyen.`);
      setError(null);
      return;
    }

    setSaving(true);
    setQuantityError(null);
    setError(null);

    try {
      const updated = await updateAdminInventory(item.productId, value, available, item.version);
      onSaved(updated);
      setEditing(false);
    } catch (saveError) {
      if (hasErrorStatus(saveError, 409)) setConflict(true);
      setError(getErrorMessage(saveError, "A mentés nem sikerült."));
    } finally {
      setSaving(false);
    }
  }

  function cancel() {
    setQuantity(String(item.stockQuantity));
    setAvailable(item.available);
    setError(null);
    setQuantityError(null);
    setEditing(false);
  }

  return (
    <article
      className={`overflow-hidden rounded-sm border bg-white shadow-sm ${
        item.available ? "border-site-border" : "border-site-muted/40"
      }`}
    >
      <div className="grid gap-0 sm:grid-cols-[10rem_minmax(0,1fr)]">
        <div className="relative aspect-[16/9] bg-site-surface-soft sm:aspect-auto">
          <img
            src={imageUrl(item.image)}
            alt=""
            className={`h-full w-full object-cover ${item.available ? "" : "opacity-65 grayscale-[35%]"}`}
          />
          <span
            className={`absolute left-2 top-2 inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-xs font-bold shadow-sm ${
              item.available ? "bg-site-green text-white" : "bg-white text-site-danger"
            }`}
          >
            <span aria-hidden="true">{item.available ? "✓" : "×"}</span>
            {item.available ? "Rendelhető" : "Nem rendelhető"}
          </span>
        </div>

        <div className="p-5">
          <div>
            <h2 className="text-lg font-bold text-site-heading">{item.name}</h2>
            <p className="mt-1 text-xs text-site-muted">Egység: {getUnitLabel(item.unit)}</p>
          </div>

          <dl className="mt-4 grid grid-cols-3 gap-2 text-center">
            <div className="rounded-sm bg-site-surface-soft p-2">
              <dt className="text-[0.68rem] text-site-muted">Teljes</dt>
              <dd className="mt-1 font-bold text-site-heading">{item.stockQuantity}</dd>
            </div>
            <div className="rounded-sm bg-site-surface-soft p-2">
              <dt className="text-[0.68rem] text-site-muted">Foglalt</dt>
              <dd className="mt-1 font-bold text-site-heading">{item.reservedQuantity}</dd>
            </div>
            <div className="rounded-sm bg-site-surface-soft p-2">
              <dt className="text-[0.68rem] text-site-muted">Szabad</dt>
              <dd className="mt-1 font-bold text-site-heading">{item.availableQuantity}</dd>
            </div>
          </dl>

          {error && (
            <p role="alert" className="mt-3 rounded-sm bg-site-danger-soft p-2 text-xs text-site-danger">
              {error}
            </p>
          )}

          {editing ? (
            <div className="mt-4 space-y-3 border-t border-site-border pt-4">
              {previousInput && <p role="status">{previousInput}</p>}
              {conflict && <button type="button" disabled={saving} onClick={() => void reloadCurrent()}>Aktuális adatok betöltése</button>}
              <label className="block text-sm font-semibold">
                Új teljes készlet
                <input
                  type="number"
                  min={item.reservedQuantity}
                  step="1"
                  value={quantity}
                  onChange={(event) => {
                    setQuantity(event.target.value);
                    if (Number(event.target.value) === 0) setAvailable(false);
                    if (quantityError) setQuantityError(null);
                  }}
                  aria-invalid={Boolean(quantityError)}
                  aria-describedby={quantityError ? quantityErrorId : undefined}
                  className={`mt-1 w-full rounded-sm border px-3 py-2 ${
                    quantityError ? "border-site-danger bg-white" : "border-site-border"
                  }`}
                />
                {quantityError && (
                  <span id={quantityErrorId} role="alert" className="mt-1 block text-xs text-site-danger">
                    {quantityError}
                  </span>
                )}
              </label>

              <label className="flex items-center gap-2 text-sm font-semibold">
                <input
                  type="checkbox"
                  checked={available}
                  disabled={Number(quantity) <= 0}
                  onChange={(event) => setAvailable(event.target.checked)}
                />
                Elérhető a vásárlóknak
              </label>

              <div className="flex gap-2">
                <button
                  type="button"
                  onClick={cancel}
                  disabled={saving}
                  className="flex-1 rounded-sm border border-site-border px-3 py-2 text-sm font-semibold"
                >
                  Mégse
                </button>
                <button
                  type="button"
                  onClick={() => void save()}
                  disabled={saving || conflict}
                  className="flex-1 rounded-sm bg-site-green px-3 py-2 text-sm font-semibold text-white disabled:opacity-60"
                >
                  {saving ? "Mentés..." : "Mentés"}
                </button>
              </div>
            </div>
          ) : (
            <button
              type="button"
              onClick={() => setEditing(true)}
              className="mt-4 w-full rounded-sm border border-site-green-dark px-3 py-2 text-sm font-semibold text-site-green-dark hover:bg-site-surface-soft"
            >
              Készlet módosítása
            </button>
          )}
        </div>
      </div>
    </article>
  );
}

export default function AdminInventoryPage() {
  const [items, setItems] = useState<AdminInventoryItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState<StockFilter>("all");
  const [page, setPage] = useState(1);

  const load = useCallback(async (signal?: AbortSignal) => {
    setLoading(true);
    setError(null);

    try {
      setItems(await getAdminInventory(signal));
    } catch (loadError) {
      if (!isAbortError(loadError)) {
        setError(getErrorMessage(loadError, "A készlet betöltése nem sikerült."));
      }
    } finally {
      if (!signal?.aborted) setLoading(false);
    }
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal);
    return () => controller.abort();
  }, [load]);

  useEffect(() => {
    if (!message) return;
    const timer = window.setTimeout(() => setMessage(null), 3500);
    return () => window.clearTimeout(timer);
  }, [message]);

  const filtered = useMemo(() => {
    const query = search.trim().toLocaleLowerCase("hu-HU");

    return items.filter((item) => {
      if (query && !item.name.toLocaleLowerCase("hu-HU").includes(query)) return false;
      if (filter === "low" && !(item.availableQuantity > 0 && item.availableQuantity <= 5)) return false;
      if (filter === "empty" && item.availableQuantity !== 0) return false;
      if (filter === "reserved" && item.reservedQuantity === 0) return false;
      return true;
    });
  }, [items, search, filter]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const currentPage = Math.min(page, totalPages);
  const pageItems = filtered.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE);

  function saved(updated: AdminInventoryItem) {
    setItems((current) =>
      current.map((item) => (item.productId === updated.productId ? updated : item)),
    );
    setMessage(`${updated.name} készlete frissült.`);
  }

  return (
    <div className="space-y-6">
      <AdminPageHeader
        title="Raktárkészlet"
        description="A teljes, lefoglalt és ténylegesen értékesíthető készlet követése és módosítása."
      />

      {message && (
        <div role="status" className="rounded-sm bg-site-surface-soft p-4 font-semibold text-site-green-dark">
          {message}
        </div>
      )}

      {error && (
        <div role="alert" className="rounded-sm bg-site-danger-soft p-4 text-site-danger">
          {error}
        </div>
      )}

      <section className="grid gap-4 rounded-sm border border-site-border bg-white p-4 shadow-sm sm:grid-cols-2 sm:p-5">
        <label className="text-sm font-semibold">
          Keresés
          <input
            value={search}
            onChange={(event) => {
              setSearch(event.target.value);
              setPage(1);
            }}
            placeholder="Termék neve"
            className="mt-1.5 w-full rounded-sm border border-site-border px-3 py-2.5"
          />
        </label>

        <label className="text-sm font-semibold">
          Készletállapot
          <select
            value={filter}
            onChange={(event) => {
              setFilter(event.target.value as StockFilter);
              setPage(1);
            }}
            className="mt-1.5 w-full rounded-sm border border-site-border px-3 py-2.5"
          >
            <option value="all">Minden termék</option>
            <option value="low">Alacsony készlet (1–5)</option>
            <option value="empty">Nincs szabad készlet</option>
            <option value="reserved">Van aktív foglalás</option>
          </select>
        </label>
      </section>

      {loading ? (
        <div className="grid gap-5 xl:grid-cols-2">
          {Array.from({ length: 6 }, (_, index) => (
            <div key={index} className="h-64 animate-pulse rounded-sm bg-white" />
          ))}
        </div>
      ) : pageItems.length === 0 ? (
        <div className="rounded-sm border border-site-border bg-white p-8 text-center">Nincs találat.</div>
      ) : (
        <section className="grid gap-5 xl:grid-cols-2">
          {pageItems.map((item) => (
            <InventoryCard key={item.productId} item={item} onSaved={saved} />
          ))}
        </section>
      )}

      {!loading && filtered.length > 0 && (
        <AdminPagination page={currentPage} totalPages={totalPages} onPageChange={setPage} />
      )}
    </div>
  );
}
