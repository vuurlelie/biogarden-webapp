import { lazy, Suspense, useCallback, useEffect, useState } from "react";
import {
  deleteAdminPost,
  getAdminPostPage,
} from "../../features/admin/api/adminPostsApi";
import type { Post, PostCategory } from "../../features/posts/types/post.types";
import AdminPageHeader from "../../shared/components/admin/AdminPageHeader";
import AdminPagination from "../../shared/components/admin/AdminPagination";
import { getPostCategoryLabel } from "../../shared/utils/postCategory";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;
const PAGE_SIZE = 12;
const AdminPostFormModal = lazy(() => import("../../features/admin/components/AdminPostFormModal"));

type StatusFilter = "all" | "published" | "draft";

function imageUrl(path: string): string {
  return `${API_BASE_URL}/${path.replace(/^\/+/, "")}`;
}

function formatDate(value: string): string {
  return new Intl.DateTimeFormat("hu-HU", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  }).format(new Date(value));
}

function excerpt(article: string): string {
  const parsed = new DOMParser().parseFromString(article, "text/html");
  const normalized = (parsed.body.textContent ?? "").replace(/\s+/g, " ").trim();
  return normalized.length > 180 ? `${normalized.slice(0, 180)}...` : normalized;
}

export default function AdminPostsPage() {
  const [posts, setPosts] = useState<Post[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [category, setCategory] = useState<"all" | PostCategory>("all");
  const [status, setStatus] = useState<StatusFilter>("all");
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(1);
  const [editingPost, setEditingPost] = useState<Post | null | undefined>(undefined);
  const [deletingPost, setDeletingPost] = useState<Post | null>(null);
  const [deleting, setDeleting] = useState(false);

  const loadPosts = useCallback(async (signal?: AbortSignal) => {
    setLoading(true);
    setError(null);

    try {
      const result = await getAdminPostPage(
        {
          page,
          pageSize: PAGE_SIZE,
          search,
          category,
          status,
        },
        signal,
      );
      setPosts(result.items);
      setTotalCount(result.totalCount);
      setTotalPages(result.totalPages);
    } catch (loadError) {
      if (!(loadError instanceof DOMException && loadError.name === "AbortError")) {
        setError(loadError instanceof Error ? loadError.message : "A bejegyzések betöltése nem sikerült.");
      }
    } finally {
      if (!signal?.aborted) setLoading(false);
    }
  }, [category, page, search, status]);

  useEffect(() => {
    const controller = new AbortController();
    void loadPosts(controller.signal);
    return () => controller.abort();
  }, [loadPosts]);

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
    if (!deletingPost) return;

    setDeleting(true);
    setError(null);

    try {
      await deleteAdminPost(deletingPost.id);
      setDeletingPost(null);
      setMessage("A bejegyzés törlése sikerült.");
      await loadPosts();
    } catch (deleteError) {
      setError(deleteError instanceof Error ? deleteError.message : "A törlés nem sikerült.");
      setDeletingPost(null);
    } finally {
      setDeleting(false);
    }
  }

  async function handleSaved(successMessage: string) {
    setEditingPost(undefined);
    setMessage(successMessage);
    await loadPosts();
  }

  function resetPage() {
    setPage(1);
  }

  return (
    <div className="space-y-6">
      <AdminPageHeader
        title="Bejegyzések"
        description="A tudástár tartalmainak létrehozása, szerkesztése és közzététele."
        actions={(
          <button
            type="button"
            onClick={() => setEditingPost(null)}
            className="rounded-sm bg-site-green px-4 py-3 text-sm font-semibold text-white hover:bg-site-green-hover"
          >
            + Új bejegyzés
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
            onClick={() => void loadPosts()}
            className="rounded-sm border border-site-danger px-3 py-2 text-sm font-semibold"
          >
            Újrapróbálás
          </button>
        </div>
      )}

      <section
        className="rounded-sm border border-site-border bg-white p-4 shadow-sm sm:p-5"
        aria-label="Bejegyzések szűrése"
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
              placeholder="Cím vagy szövegrészlet"
              className="mt-1.5 w-full rounded-sm border border-site-border px-3 py-2.5 outline-none focus:border-site-green"
            />
          </label>

          <label className="text-sm font-semibold">
            Kategória
            <select
              value={category}
              onChange={(event) => {
                setCategory(event.target.value === "all" ? "all" : Number(event.target.value) as PostCategory);
                resetPage();
              }}
              className="mt-1.5 w-full rounded-sm border border-site-border px-3 py-2.5"
            >
              <option value="all">Minden kategória</option>
              <option value="0">Tudástár</option>
              <option value="1">Receptajánló</option>
            </select>
          </label>

          <label className="text-sm font-semibold">
            Állapot
            <select
              value={status}
              onChange={(event) => {
                setStatus(event.target.value as StatusFilter);
                resetPage();
              }}
              className="mt-1.5 w-full rounded-sm border border-site-border px-3 py-2.5"
            >
              <option value="all">Mindegyik</option>
              <option value="published">Közzétéve</option>
              <option value="draft">Piszkozat</option>
            </select>
          </label>
        </div>

        <p className="mt-4 text-sm text-site-muted">
          {totalCount} bejegyzés
        </p>
      </section>

      {loading ? (
        <div className="grid gap-5 md:grid-cols-2 xl:grid-cols-3">
          {Array.from({ length: 6 }, (_, index) => (
            <div key={index} className="h-96 animate-pulse rounded-sm border border-site-border bg-white" />
          ))}
        </div>
      ) : totalCount === 0 ? (
        <section className="rounded-sm border border-site-border bg-white p-8 text-center shadow-sm">
          <h2 className="text-lg font-bold text-site-heading">Nincs találat</h2>
          <p className="mt-2 text-sm text-site-muted">Módosítsd a keresési vagy szűrési feltételeket.</p>
        </section>
      ) : (
        <section className="grid gap-5 md:grid-cols-2 xl:grid-cols-3" aria-label="Bejegyzéslista">
          {posts.map((post) => (
            <article key={post.id} className="flex min-w-0 flex-col overflow-hidden rounded-sm border border-site-border bg-white shadow-sm">
              <div className="relative aspect-[16/9] bg-site-surface-soft">
                <img src={imageUrl(post.image)} alt="" className="h-full w-full object-cover" />
                <span
                  className={`absolute left-3 top-3 rounded-full px-3 py-1 text-xs font-bold ${
                    post.draft ? "bg-white text-site-muted shadow" : "bg-site-green text-white"
                  }`}
                >
                  {post.draft ? "Piszkozat" : "Közzétéve"}
                </span>
              </div>

              <div className="flex flex-1 flex-col p-5">
                <p className="text-xs font-bold uppercase tracking-wider text-site-green-dark">
                  {getPostCategoryLabel(post.category)}
                </p>
                <h2 className="mt-1 break-words text-xl font-bold text-site-heading">{post.title}</h2>
                <p className="mt-1 text-xs text-site-muted">{formatDate(post.publishedAt)}</p>
                <p className="mt-3 line-clamp-4 text-sm leading-6 text-site-muted">{excerpt(post.article)}</p>

                <div className="mt-auto flex gap-3 pt-5">
                  <button
                    type="button"
                    onClick={() => setEditingPost(post)}
                    className="flex-1 rounded-sm bg-site-green px-4 py-2.5 font-semibold text-white hover:bg-site-green-hover"
                  >
                    Szerkesztés
                  </button>
                  <button
                    type="button"
                    onClick={() => setDeletingPost(post)}
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

      {editingPost !== undefined && (
        <Suspense fallback={<div className="fixed inset-0 z-[80] grid place-items-center bg-black/55 p-4 text-white">Szerkesztő betöltése...</div>}>
          <AdminPostFormModal
            post={editingPost}
            onClose={() => setEditingPost(undefined)}
            onSaved={(text) => void handleSaved(text)}
          />
        </Suspense>
      )}

      {deletingPost && (
        <div
          className="fixed inset-0 z-[90] grid place-items-center bg-black/55 p-4"
          role="dialog"
          aria-modal="true"
          aria-labelledby="delete-post-title"
        >
          <section className="w-full max-w-md rounded-sm bg-white p-6 shadow-xl">
            <h2 id="delete-post-title" className="text-xl font-bold text-site-heading">
              Bejegyzés törlése
            </h2>
            <p className="mt-3 leading-6 text-site-muted">
              Biztosan törölni szeretnéd ezt a bejegyzést:{" "}
              <strong className="text-site-heading">{deletingPost.title}</strong>?
            </p>
            <p className="mt-2 text-sm text-site-danger">A bejegyzés többé nem jelenik meg a látogatóknak.</p>
            <div className="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
              <button
                type="button"
                onClick={() => setDeletingPost(null)}
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
