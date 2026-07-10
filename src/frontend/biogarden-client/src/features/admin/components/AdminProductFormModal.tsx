import { useEffect, useMemo, useState, type FormEvent } from "react";
import type { ProductCategory, ProductDetails, UnitOfMeasure } from "../../products/types/product.types";
import {
  createAdminProduct,
  deleteUnusedAdminProductImage,
  updateAdminProduct,
  uploadAdminProductImage,
  type AdminProductInput,
} from "../api/adminProductsApi";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

const categories: Array<[ProductCategory, string]> = [
  [0, "Zöldség"],
  [2, "Gyógytea"],
  [3, "Illóolaj"],
  [4, "Hidrolátum"],
  [5, "Fűszer"],
];

const units: Array<[UnitOfMeasure, string]> = [
  [0, "darab"],
  [1, "kilogramm"],
  [2, "gramm"],
  [3, "milliliter"],
  [4, "liter"],
  [5, "csomag"],
  [6, "üveg"],
];

type FormState = {
  name: string;
  description: string;
  price: string;
  category: ProductCategory;
  image: string;
  unit: UnitOfMeasure;
  unitAmount: string;
  unitAmountMeasure: UnitOfMeasure;
  stockQuantity: string;
  available: boolean;
  deliveryAvailable: boolean;
};

type Props = {
  product: ProductDetails | null;
  onClose: () => void;
  onSaved: (message: string) => void;
};

function initialState(product: ProductDetails | null): FormState {
  if (!product) {
    return {
      name: "",
      description: "",
      price: "",
      category: 0,
      image: "",
      unit: 0,
      unitAmount: "",
      unitAmountMeasure: 2,
      stockQuantity: "0",
      available: false,
      deliveryAvailable: false,
    };
  }

  return {
    name: product.name,
    description: product.description ?? "",
    price: String(product.price),
    category: product.category,
    image: product.image,
    unit: product.unit,
    unitAmount: product.unitAmount?.toString() ?? "",
    unitAmountMeasure: product.unitAmountMeasure ?? 2,
    stockQuantity: String(product.stockQuantity),
    available: product.available,
    deliveryAvailable: product.deliveryAvailable,
  };
}

function imageUrl(path: string): string {
  return path ? `${API_BASE_URL}/${path.replace(/^\/+/, "")}` : "";
}

export default function AdminProductFormModal({ product, onClose, onSaved }: Props) {
  const [form, setForm] = useState(() => initialState(product));
  const [imageFile, setImageFile] = useState<File | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const previewUrl = useMemo(
    () => (imageFile ? URL.createObjectURL(imageFile) : imageUrl(form.image)),
    [imageFile, form.image],
  );

  useEffect(() => {
    document.body.style.overflow = "hidden";
    return () => {
      document.body.style.overflow = "";
    };
  }, []);

  useEffect(() => {
    return () => {
      if (imageFile && previewUrl.startsWith("blob:")) URL.revokeObjectURL(previewUrl);
    };
  }, [imageFile, previewUrl]);

  function update<K extends keyof FormState>(key: K, value: FormState[K]) {
    setForm((current) => ({ ...current, [key]: value }));
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    setError(null);

    const price = Number(form.price);
    const stockQuantity = Number(form.stockQuantity);
    const unitAmount = form.unitAmount === "" ? null : Number(form.unitAmount);

    if (form.name.trim().length < 2) {
      setError("A termék neve legalább 2 karakter legyen.");
      return;
    }
    if (!Number.isFinite(price) || price < 0) {
      setError("Adj meg érvényes, nem negatív árat.");
      return;
    }
    if (!Number.isInteger(stockQuantity) || stockQuantity < 0) {
      setError("A készlet nem negatív egész szám legyen.");
      return;
    }
    if (unitAmount !== null && (!Number.isFinite(unitAmount) || unitAmount <= 0)) {
      setError("A kiszerelés pozitív szám legyen.");
      return;
    }
    if (imageFile && imageFile.size > 5 * 1024 * 1024) {
      setError("A kép mérete legfeljebb 5 MB lehet.");
      return;
    }

    setSaving(true);
    let uploadedImage: string | null = null;

    try {
      uploadedImage = imageFile ? await uploadAdminProductImage(imageFile) : null;

      const image = uploadedImage ?? (product ? form.image.trim() : form.image.trim() || null);
      const payload: AdminProductInput = {
        name: form.name.trim(),
        description: form.description.trim() || null,
        price,
        category: form.category,
        image,
        unit: form.unit,
        unitAmount,
        unitAmountMeasure: unitAmount === null ? null : form.unitAmountMeasure,
        stockQuantity,
        available: stockQuantity > 0 && form.available,
        deliveryAvailable: form.deliveryAvailable,
      };

      if (product) {
        await updateAdminProduct(product.id, payload);
      } else {
        await createAdminProduct(payload);
      }

      onSaved(product ? "A termék módosítása sikerült." : "Az új termék létrehozása sikerült.");
    } catch (saveError) {
      if (uploadedImage) {
        try {
          await deleteUnusedAdminProductImage(uploadedImage);
        } catch {
          // The original save error is more useful to the administrator.
        }
      }
      setError(saveError instanceof Error ? saveError.message : "A mentés nem sikerült.");
    } finally {
      setSaving(false);
    }
  }

  const inputClass =
    "mt-1.5 w-full rounded-sm border border-site-border bg-white px-3 py-2.5 text-site-text outline-none focus:border-site-green focus:ring-2 focus:ring-site-green/20";

  return (
    <div
      className="fixed inset-0 z-[80] flex items-center justify-center bg-black/55 p-3 sm:p-5"
      role="dialog"
      aria-modal="true"
      aria-labelledby="product-form-title"
    >
      <section className="max-h-[95vh] w-full max-w-4xl overflow-y-auto rounded-sm bg-white shadow-xl">
        <div className="sticky top-0 z-10 flex items-center justify-between gap-4 border-b border-site-border bg-white px-5 py-4 sm:px-6">
          <div>
            <p className="text-xs font-bold uppercase tracking-wider text-site-green-dark">Termékkezelés</p>
            <h2 id="product-form-title" className="text-xl font-bold text-site-heading sm:text-2xl">
              {product ? "Termék szerkesztése" : "Új termék"}
            </h2>
          </div>
          <button
            type="button"
            onClick={onClose}
            disabled={saving}
            aria-label="Bezárás"
            className="grid h-11 w-11 place-items-center rounded-sm border border-site-border text-2xl hover:bg-site-surface-soft disabled:opacity-50"
          >
            ×
          </button>
        </div>

        <form onSubmit={submit} className="space-y-6 p-5 sm:p-6">
          {error && (
            <div role="alert" className="rounded-sm bg-site-danger-soft p-4 text-site-danger">
              {error}
            </div>
          )}

          <div className="grid gap-5 md:grid-cols-2">
            <label className="text-sm font-semibold">
              Név <span className="text-site-danger">*</span>
              <input
                className={inputClass}
                value={form.name}
                onChange={(event) => update("name", event.target.value)}
                maxLength={200}
              />
            </label>

            <label className="text-sm font-semibold">
              Kategória <span className="text-site-danger">*</span>
              <select
                className={inputClass}
                value={form.category}
                onChange={(event) => update("category", Number(event.target.value) as ProductCategory)}
              >
                {categories.map(([value, label]) => (
                  <option key={value} value={value}>{label}</option>
                ))}
              </select>
            </label>

            <label className="text-sm font-semibold">
              Ár (Ft) <span className="text-site-danger">*</span>
              <input
                className={inputClass}
                type="number"
                min="0"
                step="1"
                value={form.price}
                onChange={(event) => update("price", event.target.value)}
              />
            </label>

            <label className="text-sm font-semibold">
              Értékesítési egység <span className="text-site-danger">*</span>
              <select
                className={inputClass}
                value={form.unit}
                onChange={(event) => update("unit", Number(event.target.value) as UnitOfMeasure)}
              >
                {units.map(([value, label]) => (
                  <option key={value} value={value}>{label}</option>
                ))}
              </select>
            </label>

            <label className="text-sm font-semibold">
              Készlet <span className="text-site-danger">*</span>
              <input
                className={inputClass}
                type="number"
                min="0"
                step="1"
                value={form.stockQuantity}
                onChange={(event) => {
                  update("stockQuantity", event.target.value);
                  if (Number(event.target.value) === 0) update("available", false);
                }}
              />
            </label>

            <div className="grid grid-cols-2 gap-3">
              <label className="text-sm font-semibold">
                Kiszerelés
                <input
                  className={inputClass}
                  type="number"
                  min="0.01"
                  step="0.01"
                  value={form.unitAmount}
                  onChange={(event) => update("unitAmount", event.target.value)}
                />
              </label>

              <label className="text-sm font-semibold">
                Mértékegysége
                <select
                  className={inputClass}
                  value={form.unitAmountMeasure}
                  disabled={!form.unitAmount}
                  onChange={(event) => update("unitAmountMeasure", Number(event.target.value) as UnitOfMeasure)}
                >
                  {units.map(([value, label]) => (
                    <option key={value} value={value}>{label}</option>
                  ))}
                </select>
              </label>
            </div>
          </div>

          <label className="block text-sm font-semibold">
            Leírás
            <textarea
              className={`${inputClass} min-h-28 resize-y`}
              value={form.description}
              onChange={(event) => update("description", event.target.value)}
              maxLength={1000}
            />
          </label>

          <div className="grid items-start gap-5 md:grid-cols-[12rem_minmax(0,1fr)]">
            <div className="aspect-square overflow-hidden rounded-sm border border-site-border bg-site-surface-soft">
              {previewUrl ? (
                <img src={previewUrl} alt="Termékkép előnézete" className="h-full w-full object-cover" />
              ) : (
                <div className="grid h-full place-items-center px-4 text-center text-sm text-site-muted">
                  Nincs kiválasztott kép
                </div>
              )}
            </div>

            <div>
              <label className="block text-sm font-semibold">
                Termékkép
                <input
                  className="mt-2 block w-full rounded-sm border border-site-border p-3 text-sm file:mr-3 file:rounded-sm file:border-0 file:bg-site-surface-soft file:px-3 file:py-2 file:font-semibold file:text-site-green-dark"
                  type="file"
                  accept="image/jpeg,image/png,image/webp"
                  onChange={(event) => setImageFile(event.target.files?.[0] ?? null)}
                />
              </label>
              <p className="mt-2 text-xs text-site-muted">JPG, PNG vagy WEBP, legfeljebb 5 MB.</p>

              {(previewUrl || imageFile) && (
                <button
                  type="button"
                  onClick={() => {
                    setImageFile(null);
                    update("image", "");
                  }}
                  className="mt-4 rounded-sm border border-site-danger/40 px-4 py-2 text-sm font-semibold text-site-danger hover:bg-site-danger-soft"
                >
                  Kép eltávolítása
                </button>
              )}
            </div>
          </div>

          <div className="grid gap-3 sm:grid-cols-2">
            <label className="flex items-start gap-3 rounded-sm border border-site-border p-4">
              <input
                type="checkbox"
                className="mt-1"
                checked={form.available}
                disabled={Number(form.stockQuantity) <= 0}
                onChange={(event) => update("available", event.target.checked)}
              />
              <span>
                <strong className="block text-site-heading">Elérhető</strong>
                <span className="text-sm text-site-muted">Megjelenhet és kosárba tehető.</span>
              </span>
            </label>

            <label className="flex items-start gap-3 rounded-sm border border-site-border p-4">
              <input
                type="checkbox"
                className="mt-1"
                checked={form.deliveryAvailable}
                onChange={(event) => update("deliveryAvailable", event.target.checked)}
              />
              <span>
                <strong className="block text-site-heading">Házhoz szállítható</strong>
                <span className="text-sm text-site-muted">Szállítási rendelésbe is kerülhet.</span>
              </span>
            </label>
          </div>

          <div className="flex flex-col-reverse gap-3 border-t border-site-border pt-5 sm:flex-row sm:justify-end">
            <button
              type="button"
              onClick={onClose}
              disabled={saving}
              className="rounded-sm border border-site-border px-5 py-3 font-semibold hover:bg-site-surface-soft disabled:opacity-50"
            >
              Mégse
            </button>
            <button
              type="submit"
              disabled={saving}
              className="rounded-sm bg-site-green px-5 py-3 font-semibold text-white hover:bg-site-green-hover disabled:opacity-60"
            >
              {saving ? "Mentés..." : "Mentés"}
            </button>
          </div>
        </form>
      </section>
    </div>
  );
}
