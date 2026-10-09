import { SITE_TIME_ZONE, parseApiDate } from "../../shared/utils/dateTime";
import { lazy, Suspense, useCallback, useEffect, useRef, useState } from "react";
import {
  deleteAdminPost,
  getAdminPostPage,
} from "../../features/admin/api/adminPostsApi";
import type { Post, PostCategory } from "../../features/posts/types/post.types";
import AdminDeleteConfirmationModal from "../../shared/components/admin/AdminDeleteConfirmationModal";
import AdminFeedbackMessages from "../../shared/components/admin/AdminFeedbackMessages";
import AdminListEmptyState from "../../shared/components/admin/AdminListEmptyState";
import AdminListSkeletonGrid from "../../shared/components/admin/AdminListSkeletonGrid";
import AdminPageHeader from "../../shared/components/admin/AdminPageHeader";
import AdminPagination from "../../shared/components/admin/AdminPagination";
import { getErrorMessage } from "../../shared/utils/errorHandling";
import { getPostCategoryLabel } from "../../shared/utils/postCategory";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;
const PAGE_SIZE = 12;
const AdminPostFormModal = lazy(() => import("../../features/admin/components/AdminPostFormModal"));

type StatusFilter = "all" | "published" | "draft";

function imageUrl(path: string): string {
  return `${API_BASE_URL}/${path.replace(/^\/+/, "")}`;
}

function formatDate(value: string): string {
  return new Intl.DateTimeFormat(
    "hu-HU",
    { timeZone: SITE_TIME_ZONE,
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
    }
  ).format(parseApiDate(value));
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
  const latestLoadId = useRef(0);

  const loadPosts = useCallback(
    async () => {
      const loadId = latestLoadId.current + 1;
      latestLoadId.current = loadId;
      const isStaleLoad = () => latestLoadId.current !== loadId;

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
        );
        if (isStaleLoad())
          return;

        setPosts(result.items);
        setTotalCount(result.totalCount);
        setTotalPages(result.totalPages);
      } catch (loadError) {
        if (isStaleLoad())
          return;

        setError(getErrorMessage(loadError, "A bejegyzések betöltése nem sikerült."));
      } finally {
        if (!isStaleLoad())
          setLoading(false);
      }
    },
    [category, page, search, status]
  );

  useEffect(
    () => {
      void loadPosts();
    },
    [loadPosts]
  );

  useEffect(
    () => {
      if (!message)
        return;
      const timeout = window.setTimeout(() => setMessage(null), 4000);
      return () => window.clearTimeout(timeout);
    },
    [message]
  );

  useEffect(
    () => {
      if (page > totalPages) {
        setPage(totalPages);
      }
    },
    [page, totalPages]
  );

  async function confirmDelete() {
    if (!deletingPost)
      return;

    setDeleting(true);
    setError(null);

    try {
      await deleteAdminPost(deletingPost.id);
      setDeletingPost(null);
      setMessage("A bejegyzés törlése sikerült.");
      await loadPosts();
    } catch (deleteError) {
      setError(getErrorMessage(deleteError, "A törlés nem sikerült."));
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

      <AdminFeedbackMessages
        message={message}
        error={error}
        retryLabel="Újrapróbálás"
        onRetry={() => void loadPosts()}
      />

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
        <AdminListSkeletonGrid itemHeightClassName="h-96" />
      ) : totalCount === 0 ? (
        <AdminListEmptyState
          title="Nincs találat"
          description="Módosítsd a keresési vagy szűrési feltételeket."
        />
      ) : (
        <section className="grid gap-5 md:grid-cols-2 xl:grid-cols-3" aria-label="Bejegyzéslista">
          {posts.map((post) => (
            <article key={post.id} className="flex min-w-0 flex-col overflow-hidden rounded-sm border border-site-border bg-white shadow-sm">
              <div className="relative h-56 overflow-hidden bg-site-surface-soft sm:h-60">
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
        <AdminDeleteConfirmationModal
          title="Bejegyzés törlése"
          itemName={deletingPost.title}
          description="Biztosan törölni szeretnéd ezt a bejegyzést:"
          warning="A bejegyzés többé nem jelenik meg a látogatóknak."
          deleting={deleting}
          cancelLabel="Mégse"
          confirmLabel="Törlés"
          deletingLabel="Törlés..."
          onCancel={() => setDeletingPost(null)}
          onConfirm={() => void confirmDelete()}
        />
      )}
    </div>
  );
}
