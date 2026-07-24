import { useEffect, useMemo, useRef, useState, type FormEvent } from "react";
import type { Post, PostCategory } from "../../posts/types/post.types";
import { useModalFocus } from "../../../shared/hooks/useModalFocus";
import { getErrorMessage } from "../../../shared/utils/errorHandling";
import {
  createAdminPost,
  deleteUnusedAdminPostImage,
  updateAdminPost,
  uploadAdminPostImage,
  type AdminPostInput,
} from "../api/adminPostsApi";
import RichTextEditor from "./RichTextEditor";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;
const MAX_IMAGE_UPLOAD_SIZE_BYTES = 2 * 1024 * 1024;

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

type Props = {
  post: Post | null;
  onClose: () => void;
  onSaved: (message: string) => void;
};

function formatDateTimeLocal(value: string | null | undefined): string {
  const date = value ? new Date(value) : new Date();
  const offset = date.getTimezoneOffset();
  const local = new Date(date.getTime() - offset * 60_000);
  return local.toISOString().slice(0, 16);
}

function initialState(post: Post | null): FormState {
  if (!post) {
    return {
      title: "",
      category: 0,
      article: "",
      image: "",
      publishedAt: formatDateTimeLocal(null),
      draft: true,
    };
  }

  return {
    title: post.title,
    category: post.category,
    article: post.article,
    image: post.image,
    publishedAt: formatDateTimeLocal(post.publishedAt),
    draft: post.draft,
  };
}

function imageUrl(path: string): string {
  return path ? `${API_BASE_URL}/${path.replace(/^\/+/, "")}` : "";
}

export default function AdminPostFormModal({ post, onClose, onSaved }: Props) {
  const [form, setForm] = useState(() => initialState(post));
  const [imageFile, setImageFile] = useState<File | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const modalRef = useRef<HTMLElement | null>(null);
  const titleInputRef = useRef<HTMLInputElement | null>(null);
  const imageInputRef = useRef<HTMLInputElement | null>(null);

  const previewUrl = useMemo(
    () => (imageFile ? URL.createObjectURL(imageFile) : imageUrl(form.image)),
    [imageFile, form.image],
  );

  useModalFocus(modalRef, {
    isOpen: true,
    onClose: () => {
      if (!saving) onClose();
    },
  });

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

    if (form.title.trim().length < 3) {
      setError("A bejegyzés címe legalább 3 karakter legyen.");
      titleInputRef.current?.focus();
      return;
    }

    if (form.article.trim().length < 20) {
      setError("A bejegyzés szövege legalább 20 karakter legyen.");
      document.getElementById("post-article")?.focus();
      return;
    }

    if (imageFile && imageFile.size > MAX_IMAGE_UPLOAD_SIZE_BYTES) {
      setError("A kép mérete legfeljebb 2 MB lehet. Bejegyzésképhez kb. 1600 x 1200 px, JPG vagy WEBP ajánlott.");
      imageInputRef.current?.focus();
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
        publishedAt: new Date(form.publishedAt).toISOString(),
        draft: form.draft,
      };

      if (post) {
        await updateAdminPost(post.id, payload);
      } else {
        await createAdminPost(payload);
      }

      onSaved(post ? "A bejegyzés módosítása sikerült." : "Az új bejegyzés létrehozása sikerült.");
    } catch (saveError) {
      if (uploadedImage) {
        try {
          await deleteUnusedAdminPostImage(uploadedImage);
        } catch {
          // The original save error is more useful to the administrator.
        }
      }
      setError(getErrorMessage(saveError, "A mentés nem sikerült."));
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
          {error && (
            <div role="alert" className="rounded-sm bg-site-danger-soft p-4 text-site-danger">
              {error}
            </div>
          )}

          <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_18rem]">
            <div className="space-y-5">
              <div className="grid gap-5 sm:grid-cols-2">
                <label className="text-sm font-semibold sm:col-span-2">
                  Cím <span className="text-site-danger">*</span>
                  <input
                    ref={titleInputRef}
                    className={inputClass}
                    value={form.title}
                    onChange={(event) => update("title", event.target.value)}
                    maxLength={200}
                  />
                </label>

                <label className="text-sm font-semibold">
                  Kategória <span className="text-site-danger">*</span>
                  <select
                    className={inputClass}
                    value={form.category}
                    onChange={(event) => update("category", Number(event.target.value) as PostCategory)}
                  >
                    {categories.map(([value, label]) => (
                      <option key={value} value={value}>{label}</option>
                    ))}
                  </select>
                </label>

                <label className="text-sm font-semibold">
                  Megjelenés dátuma <span className="text-site-danger">*</span>
                  <input
                    className={inputClass}
                    type="datetime-local"
                    value={form.publishedAt}
                    onChange={(event) => update("publishedAt", event.target.value)}
                  />
                </label>
              </div>

              <RichTextEditor
                id="post-article"
                label="Bejegyzés szövege"
                value={form.article}
                onChange={(nextArticle) => update("article", nextArticle)}
                required
              />
            </div>

            <aside className="space-y-4">
              <div className="overflow-hidden rounded-sm border border-site-border bg-site-surface-soft">
                {previewUrl ? (
                  <img src={previewUrl} alt="Bejegyzés képe" className="aspect-[4/3] w-full object-cover" />
                ) : (
                  <div className="grid aspect-[4/3] place-items-center px-4 text-center text-sm text-site-muted">
                    Nincs kiválasztott kép
                  </div>
                )}
              </div>

              <label className="block text-sm font-semibold">
                Kép
                <input
                  ref={imageInputRef}
                  className="mt-2 block w-full rounded-sm border border-site-border p-3 text-sm file:mr-3 file:rounded-sm file:border-0 file:bg-site-surface-soft file:px-3 file:py-2 file:font-semibold file:text-site-green-dark"
                  type="file"
                  accept="image/jpeg,image/png,image/webp"
                  onChange={(event) => setImageFile(event.target.files?.[0] ?? null)}
                />
              </label>
              <p className="text-xs text-site-muted">
                JPG vagy WEBP ajánlott, kb. 1600 x 1200 px, legfeljebb 2 MB.
              </p>

              {(previewUrl || imageFile) && (
                <button
                  type="button"
                  onClick={() => {
                    setImageFile(null);
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
