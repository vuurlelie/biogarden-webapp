import { clearAuthSession, getAuthSession } from "../../auth/utils/authStorage";
import type { Cart, CartValidationResult } from "../types/cart.types";
import { getApiErrorMessage } from "../../../shared/api/httpClient";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

export class CartApiError extends Error {
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = "CartApiError";
    this.status = status;
  }
}

async function getErrorMessage(response: Response): Promise<string> {
  return getApiErrorMessage(
    response,
    "A művelet nem sikerült. Kérjük, próbáld újra.",
  );
}

async function cartRequest(path = "", init: RequestInit = {}): Promise<Response> {
  const auth = getAuthSession();
  const headers = new Headers(init.headers);

  if (init.body) headers.set("Content-Type", "application/json");
  if (auth) headers.set("Authorization", `Bearer ${auth.token}`);

  const response = await fetch(`${API_BASE_URL}/api/cart${path}`, {
    ...init,
    headers,
    credentials: "include",
  });

  if (response.status === 401) clearAuthSession();
  if (!response.ok) {
    throw new CartApiError(await getErrorMessage(response), response.status);
  }

  return response;
}

async function cartMutation(path: string, init: RequestInit): Promise<Cart> {
  const response = await cartRequest(path, init);
  return response.json() as Promise<Cart>;
}

export async function getCart(): Promise<Cart> {
  const response = await cartRequest();
  return response.json() as Promise<Cart>;
}

export function addCartItem(productId: number, quantity: number): Promise<Cart> {
  return cartMutation("/items", {
    method: "POST",
    body: JSON.stringify({ productId, quantity }),
  });
}

export async function getAvailableQuantityToAdd(productId: number): Promise<number> {
  const response = await cartRequest(`/products/${productId}/availability`);
  const body = await response.json() as { availableQuantity: number };
  return body.availableQuantity;
}

export function updateCartItem(cartItemId: string, quantity: number): Promise<Cart> {
  return cartMutation(`/items/${encodeURIComponent(cartItemId)}`, {
    method: "PUT",
    body: JSON.stringify({ quantity }),
  });
}

export function removeCartItem(cartItemId: string): Promise<Cart> {
  return cartMutation(`/items/${encodeURIComponent(cartItemId)}`, { method: "DELETE" });
}

export function clearCart(): Promise<Cart> {
  return cartMutation("", { method: "DELETE" });
}

export function mergeGuestCart(): Promise<Cart> {
  return cartMutation("/merge", { method: "POST" });
}

export async function validateCart(): Promise<CartValidationResult> {
  const response = await cartRequest("/validate");
  return response.json() as Promise<CartValidationResult>;
}
