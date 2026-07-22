import type {
  ProductDetails,
  ProductListItem,
} from "../types/product.types";
import { getApiErrorMessage } from "../../../shared/api/httpClient";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

export async function getProducts(
  signal?: AbortSignal,
): Promise<ProductListItem[]> {
  const response = await fetch(`${API_BASE_URL}/api/products`, { signal });

  if (!response.ok) {
    throw new Error(await getApiErrorMessage(
      response,
      "A termékek betöltése nem sikerült.",
    ));
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
    throw new Error(await getApiErrorMessage(
      response,
      "A termék részleteinek betöltése nem sikerült.",
    ));
  }

  return response.json() as Promise<ProductDetails>;
}
