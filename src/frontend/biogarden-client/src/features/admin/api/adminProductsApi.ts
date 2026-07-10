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

function authorizationHeaders(): HeadersInit {
  const auth = getAuthSession();
  if (!auth) throw new Error("A munkamenet lejárt. Jelentkezz be újra.");
  return { Authorization: `Bearer ${auth.token}` };
}

async function errorMessage(response: Response): Promise<string> {
  try {
    const body = await response.json() as { detail?: string; Detail?: string; errors?: Record<string, string[]> };
    const validationMessage = body.errors ? Object.values(body.errors).flat()[0] : null;
    return validationMessage ?? body.detail ?? body.Detail ?? "A művelet nem sikerült.";
  } catch {
    return "A művelet nem sikerült.";
  }
}

async function ensureSuccess(response: Response): Promise<void> {
  if (!response.ok) throw new Error(await errorMessage(response));
}

export async function getAdminProducts(signal?: AbortSignal): Promise<ProductDetails[]> {
  const response = await fetch(`${API_BASE_URL}/api/admin/products`, {
    headers: authorizationHeaders(),
    signal,
  });
  await ensureSuccess(response);
  return response.json() as Promise<ProductDetails[]>;
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
