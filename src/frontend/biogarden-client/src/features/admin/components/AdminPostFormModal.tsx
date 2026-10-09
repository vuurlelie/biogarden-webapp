import { POST_IMAGE_ASPECT_CLASS } from "../../../shared/constants/imageLayout";
import { budapestDateTimeInput, budapestInputToUtc } from "../../../shared/utils/dateTime";
import { normalizeImage, MAX_IMAGE_UPLOAD_BYTES } from "../../../shared/utils/normalizeImage";
import { useEffect, useRef, useState, type FormEvent } from "react";
import type { Post, PostCategory } from "../../posts/types/post.types";
import { useModalFocus } from "../../../shared/hooks/useModalFocus";
import { getErrorMessage, getServerFieldErrors } from "../../../shared/utils/errorHandling";
import {
  createAdminPost,
  updateAdminPost,
  uploadAdminPostImage,
  type AdminPostInput,
} from "../api/adminPostsApi";
import RichTextEditor from "./RichTextEditor";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;
const MAX_IMAGE_UPLOAD_SIZE_BYTES = MAX_IMAGE_UPLOAD_BYTES;
const POST_IMAGE_WIDTH = 1600;
const POST_IMAGE_HEIGHT = 1200;

const categories: Array<[PostCategory, string]> = [
  [0, "Tudástár"],
  [1, "Receptajánló"],
];

type FormState = {
  title: string;
  category: PostCategory;
  article: string;
  image: string;
  publishedAt: string;
  draft: boolean;
};

type PostInvalidField = "title" | "article" | "publishedAt" | "image";
type PostFieldErrors = Partial<Record<PostInvalidField, string>>;

type Props = {
  post: Post | null;
  onClose: () => void;
  onSaved: (message: string) => void;
};


function initialState(post: Post | null): FormState {
  if (!post) {
    return {
      title: "",
      category: 0,
      article: "",
      image: "",
      publishedAt: budapestDateTimeInput(null),
      draft: true,
    };
  }

  return {
    title: post.title,
    category: post.category,
    article: post.article,
    image: post.image,
    publishedAt: budapestDateTimeInput(post.publishedAt),
    draft: post.draft,
  };
}

function imageUrl(path: string): string {
  return path ? `${API_BASE_URL}/${path.replace(/^\/+/, "")}` : "";
}

export default function AdminPostFormModal({ post, onClose, onSaved }: Props) {
  const [form, setForm] = useState(() => initialState(post));
  const [imageFile, setImageFile] = useState<File | null>(null);
  const [selectedImagePreviewUrl, setSelectedImagePreviewUrl] = useState("");
  const [previewFailed, setPreviewFailed] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<PostFieldErrors>({});
  const [saving, setSaving] = useState(false);
  const modalRef = useRef<HTMLElement | null>(null);
  const titleInputRef = useRef<HTMLInputElement | null>(null);
  const publishedAtInputRef = useRef<HTMLInputElement | null>(null);
  const imageInputRef = useRef<HTMLInputElement | null>(null);

  const previewUrl = selectedImagePreviewUrl || imageUrl(form.image);

  useModalFocus(
    modalRef,
    {
      isOpen: true,
      onClose: () => {
        if (!saving)
          onClose();
      },
    }
  );

  useEffect(
    () => {
      setPreviewFailed(false);
    },
    [previewUrl]
  );

  useEffect(
    () => {
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
    },
    [imageFile]
  );

  function update<K extends keyof FormState>(key: K, value: FormState[K]) {
    setForm((current) => ({ ...current, [key]: value }));
  }

  function getInputClassName(field?: PostInvalidField) {
    const hasError = field ? Boolean(fieldErrors[field]) : false;
    return [
      "mt-1.5 w-full rounded-sm border bg-white px-3 py-2.5 text-site-text outline-none transition focus:ring-2",
      hasError
        ? "border-site-danger bg-site-danger-soft focus:border-site-danger focus:ring-site-danger/20"
        : "border-site-border focus:border-site-green focus:ring-site-green/20",
    ].join(" ");
  }

  function getErrorId(field: PostInvalidField) {
    return `admin-post-${field}-error`;
  }

  function renderFieldError(field: PostInvalidField) {
    if (!fieldErrors[field])
      return null;

    return (
      <p id={getErrorId(field)} className="mt-1 text-xs font-normal text-site-danger">
        {fieldErrors[field]}
      </p>
    );
  }

  function clearFieldError(field: PostInvalidField) {
    setFieldErrors((current) => {
      if (!current[field])
        return current;
      const next = { ...current };
      delete next[field];
      return next;
    });
  }

  function markInvalid(field: PostInvalidField, message: string) {
    setFormError(null);
    setFieldErrors({ [field]: message });
    const inputRefs: Record<PostInvalidField, HTMLElement | null> = {
      title: titleInputRef.current,
      article: document.getElementById("post-article"),
      publishedAt: publishedAtInputRef.current,
      image: imageInputRef.current,
    };
    inputRefs[field]?.focus();
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    setFormError(null);
    setFieldErrors({});

    if (form.title.trim().length < 3) {
      markInvalid("title", "A bejegyzés címe legalább 3 karakter legyen.");
      return;
    }

    if (form.article.trim().length < 20) {
      markInvalid("article", "A bejegyzés szövege legalább 20 karakter legyen.");
      return;
    }

    if (!form.publishedAt || !budapestInputToUtc(form.publishedAt)) {
      markInvalid("publishedAt", "Adj meg érvényes budapesti időpontot. Óraátállításkor válassz egy nem ismétlődő, létező időpontot.");
      return;
    }

    if (imageFile && imageFile.size > MAX_IMAGE_UPLOAD_SIZE_BYTES) {
      markInvalid("image", "A kép mérete legfeljebb 2 MB lehet. Bejegyzésképhez kb. 1600 x 1200 px, JPG vagy WEBP ajánlott.");
      return;
    }

    setSaving(true);
    let uploadedImage: string | null = null;

    try {
      uploadedImage = imageFile ? await uploadAdminPostImage(imageFile) : null;

      const payload: AdminPostInput = {
        title: form.title.trim(),
        category: form.category,
        article: form.article.trim(),
        image: uploadedImage ?? (post ? form.image.trim() : form.image.trim() || null),
        publishedAt: budapestInputToUtc(form.publishedAt)!,
        draft: form.draft,
      };

      if (post) {
        await updateAdminPost(post.id, payload);
      } else {
        await createAdminPost(payload);
      }

      onSaved(post ? "A bejegyzés módosítása sikerült." : "Az új bejegyzés létrehozása sikerült.");
    } catch (saveError) {
      setFieldErrors(getServerFieldErrors(
        saveError,
        ["title", "article", "publishedAt", "image"] as const
      ));
      setFormError(getErrorMessage(saveError, "A mentés nem sikerült."));
    } finally {
      setSaving(false);
    }
  }

  return (
    <div
      className="fixed inset-0 z-[80] flex items-center justify-center bg-black/55 p-3 sm:p-5"
      role="dialog"
      aria-modal="true"
      aria-labelledby="post-form-title"
    >
      <section
        ref={modalRef}
        tabIndex={-1}
        className="max-h-[95vh] w-full max-w-5xl overflow-y-auto rounded-sm bg-white shadow-xl outline-none"
      >
        <div className="sticky top-0 z-10 flex items-center justify-between gap-4 border-b border-site-border bg-white px-5 py-4 sm:px-6">
          <div>
            <p className="text-xs font-bold uppercase tracking-wider text-site-green-dark">Tudástár</p>
            <h2 id="post-form-title" className="text-xl font-bold text-site-heading sm:text-2xl">
              {post ? "Bejegyzés szerkesztése" : "Új bejegyzés"}
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
          {formError && (
            <div role="alert" className="rounded-sm bg-site-danger-soft p-4 text-site-danger">
              {formError}
            </div>
          )}

          <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_18rem]">
            <div className="space-y-5">
              <div className="grid gap-5 sm:grid-cols-2">
                <label className="text-sm font-semibold sm:col-span-2">
                  Cím <span className="text-site-danger">*</span>
                  <input
                    ref={titleInputRef}
                    className={getInputClassName("title")}
                    value={form.title}
                    onChange={(event) => {
                      update("title", event.target.value);
                      clearFieldError("title");
                    }}
                    maxLength={200}
                    aria-invalid={Boolean(fieldErrors.title)}
                    aria-describedby={fieldErrors.title ? getErrorId("title") : undefined}
                  />
                  {renderFieldError("title")}
                </label>

                <label className="text-sm font-semibold">
                  Kategória <span className="text-site-danger">*</span>
                  <select
                    className={getInputClassName()}
                    value={form.category}
                    onChange={(event) => update("category", Number(event.target.value) as PostCategory)}
                  >
                    {categories.map(([value, label]) => (
                      <option key={value} value={value}>{label}</option>
                    ))}
                  </select>
                </label>

                <label className="text-sm font-semibold">
                  Megjelenés dátuma (budapesti idő) <span className="text-site-danger">*</span>
                  <input
                    ref={publishedAtInputRef}
                    className={getInputClassName("publishedAt")}
                    type="datetime-local"
                    value={form.publishedAt}
                    onChange={(event) => {
                      update("publishedAt", event.target.value);
                      clearFieldError("publishedAt");
                    }}
                    aria-invalid={Boolean(fieldErrors.publishedAt)}
                    aria-describedby={fieldErrors.publishedAt ? getErrorId("publishedAt") : undefined}
                  />
                  {renderFieldError("publishedAt")}
                </label>
              </div>

              <RichTextEditor
                id="post-article"
                label="Bejegyzés szövege"
                value={form.article}
                onChange={(nextArticle) => {
                  update("article", nextArticle);
                  clearFieldError("article");
                }}
                error={fieldErrors.article}
                required
              />
            </div>

            <aside className="space-y-4">
              <div className="overflow-hidden rounded-sm border border-site-border bg-site-surface-soft">
                {previewUrl && !previewFailed ? (
                  <img src={previewUrl} alt="Bejegyzés képe" className={`${POST_IMAGE_ASPECT_CLASS} w-full object-cover`} />
                ) : (
                  <div className={`grid ${POST_IMAGE_ASPECT_CLASS} place-items-center px-4 text-center text-sm text-site-muted`}>
                    Nincs kiválasztott kép
                  </div>
                )}
              </div>

              <p className="text-xs text-site-muted">A bejegyzésben látható képkivágás előnézete.</p>
              <label className="block text-sm font-semibold">
                Kép
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
                    clearFieldError("image");

                    if (!selectedFile) {
                      setImageFile(null);
                      return;
                    }

                    try {
                      const normalizedFile = await normalizeImage(selectedFile, POST_IMAGE_WIDTH, POST_IMAGE_HEIGHT);
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
              <p className="text-xs text-site-muted">
                JPG vagy WEBP ajánlott, kb. 1600 x 1200 px, legfeljebb 2 MB.
              </p>

              {(previewUrl || imageFile) && (
                <button
                  type="button"
                  onClick={() => {
                    setImageFile(null);
                    setPreviewFailed(false);
                    update("image", "");
                  }}
                  className="w-full rounded-sm border border-site-danger/40 px-4 py-2 text-sm font-semibold text-site-danger hover:bg-site-danger-soft"
                >
                  Kép eltávolítása
                </button>
              )}

              <label className="flex items-start gap-3 rounded-sm border border-site-border p-4">
                <input
                  type="checkbox"
                  className="mt-1"
                  checked={!form.draft}
                  onChange={(event) => update("draft", !event.target.checked)}
                />
                <span>
                  <strong className="block text-site-heading">Közzétéve</strong>
                  <span className="text-sm text-site-muted">Ha nincs bejelölve, piszkozatként mentjük.</span>
                </span>
              </label>
            </aside>
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
