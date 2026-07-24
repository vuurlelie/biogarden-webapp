import { apiRequest, getApiErrorMessage } from "../../../shared/api/httpClient";
import type { PagedResult } from "../../../shared/types/pagination";
import type {
  ProductDetails,
  ProductListItem,
} from "../types/product.types";

export type ProductListQuery = {
  page?: number;
  pageSize?: number;
  category?: string;
  sort?: string;
  availableOnly?: boolean;
  deliveryOnly?: boolean;
};

function buildProductQuery(query: ProductListQuery = {}) {
  const params = new URLSearchParams();

  if (query.page) params.set("page", String(query.page));
  if (query.pageSize) params.set("pageSize", String(query.pageSize));
  if (query.category && query.category !== "all") params.set("category", query.category);
  if (query.sort) params.set("sort", query.sort);
  if (query.availableOnly) params.set("availableOnly", "true");
  if (query.deliveryOnly) params.set("deliveryOnly", "true");

  const queryString = params.toString();
  return queryString ? `?${queryString}` : "";
}

export async function getProductPage(
  query: ProductListQuery = {},
  signal?: AbortSignal,
): Promise<PagedResult<ProductListItem>> {
  const response = await apiRequest(`/api/products${buildProductQuery(query)}`, {
    fallbackMessage: "A termékek betöltése nem sikerült.",
    signal,
  });
  return response.json() as Promise<PagedResult<ProductListItem>>;
}

export async function getProducts(
  signal?: AbortSignal,
): Promise<ProductListItem[]> {
  const result = await getProductPage({ page: 1, pageSize: 60 }, signal);
  return result.items;
}

export async function getProductById(
  productId: number,
  signal?: AbortSignal,
): Promise<ProductDetails | null> {
  const response = await apiRequest(`/api/products/${productId}`, {
    fallbackMessage: "A termék részleteinek betöltése nem sikerült.",
    signal,
    throwOnError: false,
  });

  if (response.status === 404) return null;

  if (!response.ok) {
    throw new Error(await getApiErrorMessage(
      response,
      "A termék részleteinek betöltése nem sikerült.",
    ));
  }

  return response.json() as Promise<ProductDetails>;
}
