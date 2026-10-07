import { clearAuthSession, getAuthSession } from "../../auth/utils/authStorage";
import type { Cart, CartValidationResult } from "../types/cart.types";
import { apiRequest, type ApiRequestOptions } from "../../../shared/api/httpClient";

export class CartApiError extends Error {
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = "CartApiError";
    this.status = status;
  }
}

const CART_FALLBACK_MESSAGE = "A művelet nem sikerült. Kérjük, próbáld újra.";
const toCartError = (message: string, status: number) => new CartApiError(message, status);

async function cartRequest(path = "", init: Partial<ApiRequestOptions> = {}): Promise<Response> {
  const auth = getAuthSession();
  return apiRequest(`/api/cart${path}`, {
    fallbackMessage: CART_FALLBACK_MESSAGE,
    ...init,
    authSessionId: auth?.sessionId,
    credentials: "include",
    onUnauthorized: clearAuthSession,
    toError: toCartError,
  });
}

async function cartMutation(path: string, init: Partial<ApiRequestOptions>): Promise<Cart> {
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
    jsonBody: { productId, quantity },
  });
}

export async function getAvailableQuantityToAdd(productId: number, signal?: AbortSignal): Promise<number> {
  const response = await cartRequest(`/products/${productId}/availability`, { signal });
  const body = await response.json() as { availableQuantity: number };
  return body.availableQuantity;
}

export function updateCartItem(cartItemId: string, quantity: number): Promise<Cart> {
  return cartMutation(`/items/${encodeURIComponent(cartItemId)}`, {
    method: "PUT",
    jsonBody: { quantity },
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
