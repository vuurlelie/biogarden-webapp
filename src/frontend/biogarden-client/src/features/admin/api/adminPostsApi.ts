import { getApiErrorMessage } from "../../../shared/api/httpClient";
import type { PagedResult } from "../../../shared/types/pagination";
import { getAuthSession } from "../../auth/utils/authStorage";
import type { Post, PostCategory } from "../../posts/types/post.types";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

export type AdminPostInput = {
  title: string;
  category: PostCategory;
  article: string;
  image: string | null;
  publishedAt: string;
  draft: boolean;
};

export type AdminPostListQuery = {
  page?: number;
  pageSize?: number;
  search?: string;
  category?: "all" | PostCategory;
  status?: "all" | "published" | "draft";
};

function authorizationHeaders(): HeadersInit {
  const auth = getAuthSession();
  if (!auth) throw new Error("A munkamenet lejárt. Jelentkezz be újra.");
  return { Authorization: `Bearer ${auth.token}` };
}

function buildPostQuery(query: AdminPostListQuery = {}) {
  const params = new URLSearchParams();

  if (query.page) params.set("page", String(query.page));
  if (query.pageSize) params.set("pageSize", String(query.pageSize));
  if (query.search?.trim()) params.set("search", query.search.trim());
  if (query.category !== undefined && query.category !== "all") {
    params.set("category", String(query.category));
  }
  if (query.status === "published") params.set("draft", "false");
  if (query.status === "draft") params.set("draft", "true");

  const queryString = params.toString();
  return queryString ? `?${queryString}` : "";
}

async function errorMessage(response: Response): Promise<string> {
  return getApiErrorMessage(response, "A művelet nem sikerült.");
}

async function ensureSuccess(response: Response): Promise<void> {
  if (!response.ok) throw new Error(await errorMessage(response));
}

export async function getAdminPostPage(
  query: AdminPostListQuery = {},
  signal?: AbortSignal,
): Promise<PagedResult<Post>> {
  const response = await fetch(`${API_BASE_URL}/api/admin/posts${buildPostQuery(query)}`, {
    headers: authorizationHeaders(),
    signal,
  });
  await ensureSuccess(response);
  return response.json() as Promise<PagedResult<Post>>;
}

export async function createAdminPost(post: AdminPostInput): Promise<number> {
  const response = await fetch(`${API_BASE_URL}/api/admin/posts`, {
    method: "POST",
    headers: { ...authorizationHeaders(), "Content-Type": "application/json" },
    body: JSON.stringify(post),
  });
  await ensureSuccess(response);
  return response.json() as Promise<number>;
}

export async function updateAdminPost(postId: number, post: AdminPostInput): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/admin/posts/${postId}`, {
    method: "PUT",
    headers: { ...authorizationHeaders(), "Content-Type": "application/json" },
    body: JSON.stringify(post),
  });
  await ensureSuccess(response);
}

export async function deleteAdminPost(postId: number): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/admin/posts/${postId}`, {
    method: "DELETE",
    headers: authorizationHeaders(),
  });
  await ensureSuccess(response);
}

export async function uploadAdminPostImage(image: File): Promise<string> {
  const data = new FormData();
  data.append("image", image);

  const response = await fetch(`${API_BASE_URL}/api/admin/posts/image`, {
    method: "POST",
    headers: authorizationHeaders(),
    body: data,
  });
  await ensureSuccess(response);

  const result = await response.json() as { path: string };
  return result.path;
}

export async function deleteUnusedAdminPostImage(path: string): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/admin/posts/image`, {
    method: "DELETE",
    headers: { ...authorizationHeaders(), "Content-Type": "application/json" },
    body: JSON.stringify({ path }),
  });
  await ensureSuccess(response);
}
