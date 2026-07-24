import { apiRequest } from "../../../shared/api/httpClient";
import type { PagedResult } from "../../../shared/types/pagination";
import type { Post, PostCategory, PostSortOrder } from "../types/post.types";

export type PostListQuery = {
  page?: number;
  pageSize?: number;
  category?: "all" | PostCategory;
  sort?: PostSortOrder;
};

function buildPostQuery(query: PostListQuery = {}) {
  const params = new URLSearchParams();

  if (query.page) params.set("page", String(query.page));
  if (query.pageSize) params.set("pageSize", String(query.pageSize));
  if (query.category !== undefined && query.category !== "all") {
    params.set("category", String(query.category));
  }
  if (query.sort) params.set("sort", query.sort);

  const queryString = params.toString();
  return queryString ? `?${queryString}` : "";
}

export async function getPostPage(
  query: PostListQuery = {},
  signal?: AbortSignal,
): Promise<PagedResult<Post>> {
  const response = await apiRequest(`/api/posts${buildPostQuery(query)}`, {
    fallbackMessage: "A bejegyzések betöltése nem sikerült.",
    signal,
  });
  return response.json() as Promise<PagedResult<Post>>;
}
