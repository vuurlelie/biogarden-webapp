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

  if (query.page)
    params.set("page", String(query.page));
  if (query.pageSize)
    params.set("pageSize", String(query.pageSize));
  if (query.category !== undefined && query.category !== "all") {
    params.set("category", String(query.category));
  }
  if (query.sort)
    params.set("sort", query.sort);

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

export async function getAllPosts(): Promise<Post[]> {
  const pageSize = 60;
  const firstPage = await getPostPage({ page: 1, pageSize, sort: "newest" });

  if (firstPage.totalPages <= 1) {
    return firstPage.items;
  }

  const remainingPages = await Promise.all(
    Array.from(
      { length: firstPage.totalPages - 1 },
      (_, index) =>
        getPostPage({ page: index + 2, pageSize, sort: "newest" }),
    ),
  );

  return [
    ...firstPage.items,
    ...remainingPages.flatMap((page) => page.items),
  ];
}
