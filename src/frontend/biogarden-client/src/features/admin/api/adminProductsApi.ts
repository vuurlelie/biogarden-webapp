import { getApiErrorMessage } from "../../../shared/api/httpClient";
import type { PagedResult } from "../../../shared/types/pagination";
import { getAuthSession } from "../../auth/utils/authStorage";
import type { ProductCategory, ProductDetails, UnitOfMeasure } from "../../products/types/product.types";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

export type AdminProductInput = {
  name: string;
  description: string | null;
  price: number;
  category: ProductCategory;
  image: string | null;
  unit: UnitOfMeasure;
  unitAmount: number | null;
  unitAmountMeasure: UnitOfMeasure | null;
  stockQuantity: number;
  available: boolean;
  deliveryAvailable: boolean;
};

export type AdminProductListQuery = {
  page?: number;
  pageSize?: number;
  search?: string;
  category?: "all" | ProductCategory;
  availability?: "all" | "available" | "unavailable";
};

function authorizationHeaders(): HeadersInit {
  const auth = getAuthSession();
  if (!auth) throw new Error("A munkamenet lejárt. Jelentkezz be újra.");
  return { Authorization: `Bearer ${auth.token}` };
}

function buildProductQuery(query: AdminProductListQuery = {}) {
  const params = new URLSearchParams();

  if (query.page) params.set("page", String(query.page));
  if (query.pageSize) params.set("pageSize", String(query.pageSize));
  if (query.search?.trim()) params.set("search", query.search.trim());
  if (query.category !== undefined && query.category !== "all") {
    params.set("category", String(query.category));
  }
  if (query.availability === "available") params.set("availableOnly", "true");
  if (query.availability === "unavailable") params.set("unavailableOnly", "true");

  const queryString = params.toString();
  return queryString ? `?${queryString}` : "";
}

async function errorMessage(response: Response): Promise<string> {
  return getApiErrorMessage(response, "A művelet nem sikerült.");
}

async function ensureSuccess(response: Response): Promise<void> {
  if (!response.ok) throw new Error(await errorMessage(response));
}

export async function getAdminProductPage(
  query: AdminProductListQuery = {},
  signal?: AbortSignal,
): Promise<PagedResult<ProductDetails>> {
  const response = await fetch(`${API_BASE_URL}/api/admin/products${buildProductQuery(query)}`, {
    headers: authorizationHeaders(),
    signal,
  });
  await ensureSuccess(response);
  return response.json() as Promise<PagedResult<ProductDetails>>;
}

export async function createAdminProduct(product: AdminProductInput): Promise<number> {
  const response = await fetch(`${API_BASE_URL}/api/admin/products`, {
    method: "POST",
    headers: { ...authorizationHeaders(), "Content-Type": "application/json" },
    body: JSON.stringify(product),
  });
  await ensureSuccess(response);
  return response.json() as Promise<number>;
}

export async function updateAdminProduct(productId: number, product: AdminProductInput): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/admin/products/${productId}`, {
    method: "PUT",
    headers: { ...authorizationHeaders(), "Content-Type": "application/json" },
    body: JSON.stringify(product),
  });
  await ensureSuccess(response);
}

export async function deleteAdminProduct(productId: number): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/admin/products/${productId}`, {
    method: "DELETE",
    headers: authorizationHeaders(),
  });
  await ensureSuccess(response);
}

export async function uploadAdminProductImage(image: File): Promise<string> {
  const data = new FormData();
  data.append("image", image);
  const response = await fetch(`${API_BASE_URL}/api/admin/products/image`, {
    method: "POST",
    headers: authorizationHeaders(),
    body: data,
  });
  await ensureSuccess(response);
  const result = await response.json() as { path: string };
  return result.path;
}

export async function deleteUnusedAdminProductImage(path: string): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/admin/products/image`, {
    method: "DELETE",
    headers: { ...authorizationHeaders(), "Content-Type": "application/json" },
    body: JSON.stringify({ path }),
  });
  await ensureSuccess(response);
}
