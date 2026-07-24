import type { PagedResult } from "../../../shared/types/pagination";
import type { Post, PostCategory } from "../../posts/types/post.types";
import { adminRequest } from "./adminApiClient";

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

export async function getAdminPostPage(
  query: AdminPostListQuery = {},
  signal?: AbortSignal,
): Promise<PagedResult<Post>> {
  const response = await adminRequest(`/posts${buildPostQuery(query)}`, {
    signal,
  });
  return response.json() as Promise<PagedResult<Post>>;
}

export async function createAdminPost(post: AdminPostInput): Promise<number> {
  const response = await adminRequest("/posts", {
    method: "POST",
    jsonBody: post,
  });
  return response.json() as Promise<number>;
}

export async function updateAdminPost(postId: number, post: AdminPostInput): Promise<void> {
  await adminRequest(`/posts/${postId}`, {
    method: "PUT",
    jsonBody: post,
  });
}

export async function deleteAdminPost(postId: number): Promise<void> {
  await adminRequest(`/posts/${postId}`, {
    method: "DELETE",
  });
}

export async function uploadAdminPostImage(image: File): Promise<string> {
  const data = new FormData();
  data.append("image", image);

  const response = await adminRequest("/posts/image", {
    method: "POST",
    body: data,
  });

  const result = await response.json() as { path: string };
  return result.path;
}

export async function deleteUnusedAdminPostImage(path: string): Promise<void> {
  await adminRequest("/posts/image", {
    method: "DELETE",
    jsonBody: { path },
  });
}
