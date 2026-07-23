import { getApiErrorMessage } from "../../../shared/api/httpClient";
import type { PagedResult } from "../../../shared/types/pagination";
import type {
  ProductDetails,
  ProductListItem,
} from "../types/product.types";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

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
  const response = await fetch(`${API_BASE_URL}/api/products${buildProductQuery(query)}`, { signal });

  if (!response.ok) {
    throw new Error(await getApiErrorMessage(
      response,
      "A termékek betöltése nem sikerült.",
    ));
  }

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
  const response = await fetch(`${API_BASE_URL}/api/products/${productId}`, {
    signal,
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
