import { useEffect, useRef, useState, type FormEvent } from "react";
import type { ProductCategory, ProductDetails, UnitOfMeasure } from "../../products/types/product.types";
import { useModalFocus } from "../../../shared/hooks/useModalFocus";
import { getErrorMessage, getServerFieldErrors, hasErrorStatus } from "../../../shared/utils/errorHandling";
import { getProductById } from "../../products/api/productsApi";
import {
  createAdminProduct,
  updateAdminProduct,
  uploadAdminProductImage,
  type AdminProductInput,
} from "../api/adminProductsApi";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;
const MAX_IMAGE_UPLOAD_SIZE_BYTES = 2 * 1024 * 1024;
const PRODUCT_IMAGE_SIZE = 1200;
const defaultProductImagePath = "/uploads/products/defaultimg.png";

const categories: Array<[ProductCategory, string]> = [
  [0, "Zöldség"],
  [2, "Gyógytea"],
  [3, "Illóolaj"],
  [4, "Hidrolátum"],
  [5, "Fűszer"],
];

const vegetableCategory: ProductCategory = 0;
const kilogramUnit: UnitOfMeasure = 1;
const defaultPackageMeasure: UnitOfMeasure = 2;

const salesUnits: Array<[UnitOfMeasure, string]> = [
  [0, "darab"],
  [5, "csomag"],
  [6, "üveg"],
  [1, "kilogramm"],
];

const packageMeasures: Array<[UnitOfMeasure, string]> = [
  [1, "kilogramm"],
  [2, "gramm"],
  [3, "milliliter"],
  [4, "liter"],
];

const packageMeasureValues = new Set(packageMeasures.map(([value]) => value));

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

type ProductInvalidField = "name" | "price" | "stockQuantity" | "unitAmount" | "image";
type ProductFieldErrors = Partial<Record<ProductInvalidField, string>>;

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
      category: vegetableCategory,
      image: "",
      unit: 0,
      unitAmount: "",
      unitAmountMeasure: defaultPackageMeasure,
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
    unitAmount: product.unit === kilogramUnit ? "" : product.unitAmount?.toString() ?? "",
    unitAmountMeasure: normalizePackageMeasure(product.unitAmountMeasure),
    stockQuantity: String(product.stockQuantity),
    available: product.available,
    deliveryAvailable: product.category === vegetableCategory ? false : product.deliveryAvailable,
  };
}

function normalizePackageMeasure(unit: UnitOfMeasure | null | undefined): UnitOfMeasure {
  return unit !== null && unit !== undefined && packageMeasureValues.has(unit) ? unit : defaultPackageMeasure;
}

function imageUrl(path: string): string {
  return path ? `${API_BASE_URL}/${path.replace(/^\/+/, "")}` : "";
}

function loadSelectedImage(file: File): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();

    reader.onload = () => {
      if (typeof reader.result !== "string") {
        reject(new Error("A kiválasztott kép nem tölthető be."));
        return;
      }

      const image = new Image();
      image.onload = () => resolve(image);
      image.onerror = () => reject(new Error("A kiválasztott kép nem tölthető be."));
      image.src = reader.result;
    };
    reader.onerror = () => reject(new Error("A kiválasztott kép nem tölthető be."));
    reader.readAsDataURL(file);
  });
}

function canvasToJpegFile(canvas: HTMLCanvasElement, originalFile: File, quality: number): Promise<File> {
  return new Promise((resolve, reject) => {
    canvas.toBlob(
      (blob) => {
        if (!blob) {
          reject(new Error("A kép feldolgozása nem sikerült."));
          return;
        }

        const fileName = originalFile.name.replace(/\.[^.]+$/, "") || "termekkep";
        resolve(new File([blob], `${fileName}.jpg`, { type: "image/jpeg" }));
      },
      "image/jpeg",
      quality,
    );
  });
}

async function normalizeProductImageFile(file: File): Promise<File> {
  const image = await loadSelectedImage(file);
  const canvas = document.createElement("canvas");
  canvas.width = PRODUCT_IMAGE_SIZE;
  canvas.height = PRODUCT_IMAGE_SIZE;

  const context = canvas.getContext("2d");
  if (!context) throw new Error("A kép feldolgozása nem támogatott ebben a böngészőben.");

  const sourceSize = Math.min(image.naturalWidth, image.naturalHeight);
  const sourceX = Math.max(0, (image.naturalWidth - sourceSize) / 2);
  const sourceY = Math.max(0, (image.naturalHeight - sourceSize) / 2);

  context.fillStyle = "#ffffff";
  context.fillRect(0, 0, PRODUCT_IMAGE_SIZE, PRODUCT_IMAGE_SIZE);
  context.drawImage(
    image,
    sourceX,
    sourceY,
    sourceSize,
    sourceSize,
    0,
    0,
    PRODUCT_IMAGE_SIZE,
    PRODUCT_IMAGE_SIZE,
  );

  let normalizedFile = await canvasToJpegFile(canvas, file, 0.9);
  for (const quality of [0.82, 0.74, 0.66]) {
    if (normalizedFile.size <= MAX_IMAGE_UPLOAD_SIZE_BYTES) break;
    normalizedFile = await canvasToJpegFile(canvas, file, quality);
  }

  return normalizedFile;
}

export default function AdminProductFormModal({ product, onClose, onSaved }: Props) {
  const [form, setForm] = useState(() => initialState(product));
  const [version, setVersion] = useState(product?.version);
  const [conflict, setConflict] = useState(false);
  const [previousForm, setPreviousForm] = useState<FormState | null>(null);
  async function reloadCurrent() {
    if (!product) return;
    setSaving(true);
    try {
      const current = await getProductById(product.id);
      if (!current) throw new Error("A termék már nem érhető el.");
      setPreviousForm(form); setForm(initialState(current)); setVersion(current.version);
      setImageFile(null); setConflict(false); setFormError(null); setFieldErrors({});
    } catch (error) { setFormError(getErrorMessage(error, "A frissítés nem sikerült.")); }
    finally { setSaving(false); }
  }
  const [imageFile, setImageFile] = useState<File | null>(null);
  const [selectedImagePreviewUrl, setSelectedImagePreviewUrl] = useState("");
  const [formError, setFormError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<ProductFieldErrors>({});
  const [previewFailed, setPreviewFailed] = useState(false);
  const [defaultPreviewFailed, setDefaultPreviewFailed] = useState(false);
  const [saving, setSaving] = useState(false);
  const modalRef = useRef<HTMLElement | null>(null);
  const nameInputRef = useRef<HTMLInputElement | null>(null);
  const priceInputRef = useRef<HTMLInputElement | null>(null);
  const stockInputRef = useRef<HTMLInputElement | null>(null);
  const unitAmountInputRef = useRef<HTMLInputElement | null>(null);
  const imageInputRef = useRef<HTMLInputElement | null>(null);

  const savedImagePreviewUrl = imageUrl(form.image);
  const previewUrl = selectedImagePreviewUrl || savedImagePreviewUrl;

  const defaultPreviewUrl = imageUrl(defaultProductImagePath);
  const isSelectedImagePreview = Boolean(selectedImagePreviewUrl);
  const shouldShowPreview = Boolean(previewUrl) && (isSelectedImagePreview || !previewFailed);
  const shouldShowDefaultPreview = Boolean(savedImagePreviewUrl) && !isSelectedImagePreview && previewFailed && !defaultPreviewFailed;

  useEffect(() => {
    setPreviewFailed(false);
    setDefaultPreviewFailed(false);
  }, [previewUrl]);

  useModalFocus(modalRef, {
    isOpen: true,
    onClose: () => {
      if (!saving) onClose();
    },
  });

  useEffect(() => {
    if (!imageFile) {
      setSelectedImagePreviewUrl("");
      return;
    }

    let cancelled = false;
    const reader = new FileReader();

    reader.onload = () => {
      if (!cancelled && typeof reader.result === "string") {
        setSelectedImagePreviewUrl(reader.result);
      }
    };

    reader.onerror = () => {
      if (!cancelled) {
        setSelectedImagePreviewUrl("");
        setPreviewFailed(true);
      }
    };

    reader.readAsDataURL(imageFile);

    return () => {
      cancelled = true;
      if (reader.readyState === FileReader.LOADING) {
        reader.abort();
      }
    };
  }, [imageFile]);

  function update<K extends keyof FormState>(key: K, value: FormState[K]) {
    setForm((current) => ({ ...current, [key]: value }));
  }

  function getInputClassName(field?: ProductInvalidField) {
    const hasError = field ? Boolean(fieldErrors[field]) : false;
    return [
      "mt-1.5 w-full rounded-sm border bg-white px-3 py-2.5 text-site-text outline-none transition focus:ring-2",
      hasError
        ? "border-site-danger bg-site-danger-soft focus:border-site-danger focus:ring-site-danger/20"
        : "border-site-border focus:border-site-green focus:ring-site-green/20",
    ].join(" ");
  }

  function getErrorId(field: ProductInvalidField) {
    return `admin-product-${field}-error`;
  }

  function renderFieldError(field: ProductInvalidField) {
    if (!fieldErrors[field]) return null;

    return (
      <p id={getErrorId(field)} className="mt-1 text-xs font-semibold text-site-danger">
        {fieldErrors[field]}
      </p>
    );
  }

  function clearFieldError(field: ProductInvalidField) {
    setFieldErrors((current) => {
      if (!current[field]) return current;
      const next = { ...current };
      delete next[field];
      return next;
    });
  }

  function markInvalid(field: ProductInvalidField, message: string) {
    setFormError(null);
    setFieldErrors({ [field]: message });
    const inputRefs: Record<ProductInvalidField, HTMLElement | null> = {
      name: nameInputRef.current,
      price: priceInputRef.current,
      stockQuantity: stockInputRef.current,
      unitAmount: unitAmountInputRef.current,
      image: imageInputRef.current,
    };
    inputRefs[field]?.focus();
  }

  function updateUnit(unit: UnitOfMeasure) {
    setForm((current) => ({
      ...current,
      unit,
      unitAmount: unit === kilogramUnit ? "" : current.unitAmount,
      unitAmountMeasure: unit === kilogramUnit ? defaultPackageMeasure : normalizePackageMeasure(current.unitAmountMeasure),
    }));
  }

  function updateCategory(category: ProductCategory) {
    setForm((current) => ({
      ...current,
      category,
      deliveryAvailable:
        category === vegetableCategory
          ? false
          : current.category === vegetableCategory
            ? true
            : current.deliveryAvailable,
    }));
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (saving || conflict) return;
    setFormError(null);
    setFieldErrors({});

    const price = Number(form.price);
    const stockQuantity = Number(form.stockQuantity);
    const canSetPackage = form.unit !== kilogramUnit;
    const unitAmount = !canSetPackage || form.unitAmount === "" ? null : Number(form.unitAmount);

    if (form.name.trim().length < 2) {
      markInvalid("name", "A termék neve legalább 2 karakter legyen.");
      return;
    }
    if (!Number.isFinite(price) || price < 0 || !Number.isInteger(price)) {
      markInvalid("price", "Adj meg érvényes, nem negatív egész forint árat.");
      return;
    }
    if (price > 10000000) {
      markInvalid("price", "Az ár legfeljebb 10 000 000 Ft lehet.");
      return;
    }
    if (!Number.isInteger(stockQuantity) || stockQuantity < 0) {
      markInvalid("stockQuantity", "A készlet nem negatív egész szám legyen.");
      return;
    }
    if (unitAmount !== null && (!Number.isFinite(unitAmount) || unitAmount <= 0)) {
      markInvalid("unitAmount", "A kiszerelés pozitív szám legyen.");
      return;
    }
    if (unitAmount !== null && unitAmount > 10000000) {
      markInvalid("unitAmount", "A kiszerelés legfeljebb 10 000 000 lehet.");
      return;
    }
    if (imageFile && imageFile.size > MAX_IMAGE_UPLOAD_SIZE_BYTES) {
      markInvalid("image", "A kép mérete legfeljebb 2 MB lehet. Termékképnél kb. 1200 x 1200 px, JPG vagy WEBP ajánlott.");
      return;
    }

    setSaving(true);
    let uploadedImage: string | null = null;

    try {
      uploadedImage = imageFile ? await uploadAdminProductImage(imageFile) : null;

      const image = uploadedImage ?? (product ? form.image.trim() : form.image.trim() || null);
      const payload: AdminProductInput = {
        version,
        name: form.name.trim(),
        description: form.description.trim() || null,
        price,
        category: form.category,
        image,
        unit: form.unit,
        unitAmount,
        unitAmountMeasure: unitAmount === null ? null : normalizePackageMeasure(form.unitAmountMeasure),
        stockQuantity,
        available: stockQuantity > 0 && form.available,
        deliveryAvailable: form.category === vegetableCategory ? false : form.deliveryAvailable,
      };

      if (product) {
        await updateAdminProduct(product.id, payload);
      } else {
        await createAdminProduct(payload);
      }

      onSaved(product ? "A termék módosítása sikerült." : "Az új termék létrehozása sikerült.");
    } catch (saveError) {
      if (hasErrorStatus(saveError, 409)) setConflict(true);
      setFieldErrors(getServerFieldErrors(saveError, ["name", "price", "stockQuantity", "unitAmount", "image"] as const));
      setFormError(getErrorMessage(saveError, "A mentés nem sikerült. Kérjük, ellenőrizd a megadott adatokat."));
    } finally {
      setSaving(false);
    }
  }

  return (
    <div
      className="fixed inset-0 z-[80] flex items-center justify-center bg-black/55 p-3 sm:p-5"
      role="dialog"
      aria-modal="true"
      aria-labelledby="product-form-title"
    >
      <section
        ref={modalRef}
        tabIndex={-1}
        className="max-h-[95vh] w-full max-w-4xl overflow-y-auto rounded-sm bg-white shadow-xl outline-none"
      >
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
          {conflict && <button type="button" disabled={saving} onClick={() => void reloadCurrent()}>Aktuális adatok betöltése</button>}
          {previousForm && <details className="rounded-sm border p-3"><summary>Korábbi bevitel – ellenőrizd, majd alkalmazd újra a szükséges módosításokat</summary>
            <p>{previousForm.name} — {previousForm.price} Ft — {previousForm.stockQuantity} db</p>
            <p>{previousForm.description}</p>
            <p>Kategória: {categories.find(([id]) => id === previousForm.category)?.[1]}; egység: {salesUnits.find(([id]) => id === previousForm.unit)?.[1]}; kiszerelés: {previousForm.unitAmount} {packageMeasures.find(([id]) => id === previousForm.unitAmountMeasure)?.[1]}</p>
            <p>Rendelhető: {previousForm.available ? "igen" : "nem"}; szállítható: {previousForm.deliveryAvailable ? "igen" : "nem"}</p>
            <p>Kép: {previousForm.image || "alapértelmezett"}. A kiválasztott új képfájlt szükség esetén válaszd ki újra.</p>
          </details>}
          {formError && (
            <div role="alert" className="rounded-sm bg-site-danger-soft p-4 text-site-danger">
              {formError}
            </div>
          )}

          <div className="grid gap-5 md:grid-cols-2">
            <label className="text-sm font-semibold">
              Név <span className="text-site-danger">*</span>
              <input
                ref={nameInputRef}
                className={getInputClassName("name")}
                value={form.name}
                onChange={(event) => {
                  update("name", event.target.value);
                  clearFieldError("name");
                }}
                maxLength={200}
                aria-invalid={Boolean(fieldErrors.name)}
                aria-describedby={fieldErrors.name ? getErrorId("name") : undefined}
              />
              {renderFieldError("name")}
            </label>

            <label className="text-sm font-semibold">
              Kategória <span className="text-site-danger">*</span>
              <select
                className={getInputClassName()}
                value={form.category}
                onChange={(event) => updateCategory(Number(event.target.value) as ProductCategory)}
              >
                {categories.map(([value, label]) => (
                  <option key={value} value={value}>{label}</option>
                ))}
              </select>
            </label>

            <label className="text-sm font-semibold">
              Ár (Ft) <span className="text-site-danger">*</span>
              <input
                ref={priceInputRef}
                className={getInputClassName("price")}
                type="number"
                min="0"
                step="1"
                value={form.price}
                onWheel={(event) => event.currentTarget.blur()}
                onChange={(event) => {
                  update("price", event.target.value);
                  clearFieldError("price");
                }}
                aria-invalid={Boolean(fieldErrors.price)}
                aria-describedby={fieldErrors.price ? getErrorId("price") : undefined}
              />
              {renderFieldError("price")}
            </label>

            <label className="text-sm font-semibold">
              Értékesítési egység <span className="text-site-danger">*</span>
              <select
                className={getInputClassName()}
                value={form.unit}
                onChange={(event) => updateUnit(Number(event.target.value) as UnitOfMeasure)}
              >
                {salesUnits.map(([value, label]) => (
                  <option key={value} value={value}>{label}</option>
                ))}
              </select>
            </label>

            <label className="text-sm font-semibold">
              Készlet <span className="text-site-danger">*</span>
              <input
                ref={stockInputRef}
                className={getInputClassName("stockQuantity")}
                type="number"
                min="0"
                step="1"
                value={form.stockQuantity}
                onWheel={(event) => event.currentTarget.blur()}
                onChange={(event) => {
                  update("stockQuantity", event.target.value);
                  clearFieldError("stockQuantity");
                  if (Number(event.target.value) === 0) update("available", false);
                }}
                aria-invalid={Boolean(fieldErrors.stockQuantity)}
                aria-describedby={fieldErrors.stockQuantity ? getErrorId("stockQuantity") : undefined}
              />
              {renderFieldError("stockQuantity")}
            </label>

            {form.unit === kilogramUnit ? (
              <p className="rounded border border-site-border bg-site-soft px-4 py-3 text-sm text-site-muted">
                Kilogrammos értékesítésnél nincs külön kiszerelés.
              </p>
            ) : (
              <div className="grid grid-cols-2 gap-3">
                <label className="text-sm font-semibold">
                  Kiszerelés
                  <input
                    ref={unitAmountInputRef}
                    className={getInputClassName("unitAmount")}
                    type="number"
                    min="0.01"
                    step="0.01"
                    value={form.unitAmount}
                    onWheel={(event) => event.currentTarget.blur()}
                    onChange={(event) => {
                      update("unitAmount", event.target.value);
                      clearFieldError("unitAmount");
                    }}
                    aria-invalid={Boolean(fieldErrors.unitAmount)}
                    aria-describedby={fieldErrors.unitAmount ? getErrorId("unitAmount") : undefined}
                  />
                  {renderFieldError("unitAmount")}
                </label>

                <label className="text-sm font-semibold">
                  Mértékegység
                  <select
                    className={getInputClassName()}
                    value={form.unitAmountMeasure}
                    disabled={!form.unitAmount}
                    onChange={(event) => update("unitAmountMeasure", Number(event.target.value) as UnitOfMeasure)}
                  >
                    {packageMeasures.map(([value, label]) => (
                      <option key={value} value={value}>{label}</option>
                    ))}
                  </select>
                </label>
              </div>
            )}
          </div>

          <label className="block text-sm font-semibold">
            Leírás
            <textarea
              className={`${getInputClassName()} min-h-28 resize-y`}
              value={form.description}
              onChange={(event) => update("description", event.target.value)}
              maxLength={1000}
            />
          </label>

          <div className="grid items-start gap-5 md:grid-cols-[12rem_minmax(0,1fr)]">
            <div className="aspect-square overflow-hidden rounded-sm border border-site-border bg-site-surface-soft">
              {shouldShowPreview ? (
                <img
                  key={previewUrl}
                  src={previewUrl}
                  alt="Termékkép előnézete"
                  className="h-full w-full object-cover"
                  onError={() => setPreviewFailed(true)}
                />
              ) : shouldShowDefaultPreview ? (
                <img
                  key={defaultPreviewUrl}
                  src={defaultPreviewUrl}
                  alt="Alapértelmezett termékkép előnézete"
                  className="h-full w-full object-cover"
                  onError={() => setDefaultPreviewFailed(true)}
                />
              ) : (
                <div className="grid h-full place-items-center px-4 text-center text-sm text-site-muted">
                  {previewUrl ? "A kép előnézete nem tölthető be." : "Nincs kiválasztott kép"}
                </div>
              )}
            </div>

            <div>
              <label className="block text-sm font-semibold">
                Termékkép
                <input
                  ref={imageInputRef}
                  className={`mt-2 block w-full rounded-sm border p-3 text-sm file:mr-3 file:rounded-sm file:border-0 file:bg-site-surface-soft file:px-3 file:py-2 file:font-semibold file:text-site-green-dark ${
                    fieldErrors.image ? "border-site-danger bg-site-danger-soft" : "border-site-border"
                  }`}
                  type="file"
                  accept="image/jpeg,image/png,image/webp"
                  onChange={async (event) => {
                    const input = event.currentTarget;
                    const selectedFile = input.files?.[0] ?? null;
                    setPreviewFailed(false);
                    setDefaultPreviewFailed(false);
                    clearFieldError("image");

                    if (!selectedFile) {
                      setImageFile(null);
                      return;
                    }

                    try {
                      const normalizedFile = await normalizeProductImageFile(selectedFile);
                      if (normalizedFile.size > MAX_IMAGE_UPLOAD_SIZE_BYTES) {
                        input.value = "";
                        setImageFile(null);
                        markInvalid("image", "A kép mérete feldolgozás után is túl nagy. Válassz kisebb képet.");
                        return;
                      }

                      setImageFile(normalizedFile);
                    } catch (processingError) {
                      input.value = "";
                      setImageFile(null);
                      markInvalid("image", getErrorMessage(processingError, "A kép feldolgozása nem sikerült."));
                    }
                  }}
                  aria-invalid={Boolean(fieldErrors.image)}
                  aria-describedby={fieldErrors.image ? getErrorId("image") : undefined}
                />
                {renderFieldError("image")}
              </label>
              <p className="mt-2 text-xs text-site-muted">
                JPG vagy WEBP ajánlott, kb. 1200 x 1200 px, legfeljebb 2 MB.
              </p>

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

            <label
              className={`flex items-start gap-3 rounded-sm border border-site-border p-4 ${
                form.category === vegetableCategory
                  ? "cursor-not-allowed bg-transparent text-site-muted hover:bg-transparent"
                  : ""
              }`}
            >
              <input
                type="checkbox"
                className="mt-1"
                checked={form.category === vegetableCategory ? false : form.deliveryAvailable}
                disabled={form.category === vegetableCategory}
                onChange={(event) => update("deliveryAvailable", event.target.checked)}
              />
              <span>
                <strong className={`block ${form.category === vegetableCategory ? "text-site-muted" : "text-site-heading"}`}>
                  Házhoz szállítható
                </strong>
                <span className="text-sm text-site-muted">
                  {form.category === vegetableCategory
                    ? "Zöldségnél nem választható házhoz szállítás."
                    : "Szállítási rendelésbe is kerülhet."}
                </span>
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
              disabled={saving || conflict}
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
