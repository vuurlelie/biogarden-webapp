import { createContext, useContext } from "react";
import type { Cart } from "../types/cart.types";

export type CartContextValue = {
  cart: Cart | null;
  isLoading: boolean;
  error: string | null;
  refresh: () => Promise<void>;
  addItem: (productId: number, quantity?: number) => Promise<void>;
  updateQuantity: (cartItemId: string, quantity: number) => Promise<void>;
  removeItem: (cartItemId: string) => Promise<void>;
  clear: () => Promise<void>;
};

export const CartContext = createContext<CartContextValue | null>(null);

export function useCart(): CartContextValue {
  const context = useContext(CartContext);
  if (!context)
    throw new Error("A useCart csak CartProvider alatt használható.");
  return context;
}
