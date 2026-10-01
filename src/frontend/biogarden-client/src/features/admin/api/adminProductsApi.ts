import type { PagedResult } from "../../../shared/types/pagination";
import type { ProductCategory, ProductDetails, UnitOfMeasure } from "../../products/types/product.types";
import { adminRequest } from "./adminApiClient";

export type AdminProductInput = {
  version?: string;
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

export async function getAdminProductPage(
  query: AdminProductListQuery = {},
  signal?: AbortSignal,
): Promise<PagedResult<ProductDetails>> {
  const response = await adminRequest(`/products${buildProductQuery(query)}`, {
    signal,
  });
  return response.json() as Promise<PagedResult<ProductDetails>>;
}

export async function createAdminProduct(product: AdminProductInput): Promise<number> {
  const response = await adminRequest("/products", {
    method: "POST",
    jsonBody: product,
  });
  return response.json() as Promise<number>;
}

export async function updateAdminProduct(productId: number, product: AdminProductInput): Promise<void> {
  await adminRequest(`/products/${productId}`, {
    method: "PUT",
    jsonBody: product,
  });
}

export async function deleteAdminProduct(productId: number): Promise<void> {
  await adminRequest(`/products/${productId}`, {
    method: "DELETE",
  });
}

export async function uploadAdminProductImage(image: File): Promise<string> {
  const data = new FormData();
  data.append("image", image);
  const response = await adminRequest("/products/image", {
    method: "POST",
    body: data,
  });
  const result = await response.json() as { path: string };
  return result.path;
}
