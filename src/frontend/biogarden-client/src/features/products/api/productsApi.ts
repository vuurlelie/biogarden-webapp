import type {
  ProductDetails,
  ProductListItem,
} from "../types/product.types";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

export async function getProducts(
  signal?: AbortSignal,
): Promise<ProductListItem[]> {
  const response = await fetch(`${API_BASE_URL}/api/products`, { signal });

  if (!response.ok) {
    throw new Error("Failed to fetch products.");
  }

  return response.json() as Promise<ProductListItem[]>;
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
    throw new Error("Failed to fetch product details.");
  }

  return response.json() as Promise<ProductDetails>;
}
