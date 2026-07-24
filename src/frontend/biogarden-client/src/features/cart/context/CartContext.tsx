import { useCallback, useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import { AUTH_CHANGED_EVENT, getAuthSession } from "../../auth/utils/authStorage";
import {
  addCartItem,
  clearCart,
  getCart,
  mergeGuestCart,
  removeCartItem,
  updateCartItem,
} from "../api/cartApi";
import type { Cart } from "../types/cart.types";
import { CartContext, type CartContextValue } from "./useCart";
import { getErrorMessage } from "../../../shared/utils/errorHandling";

export function CartProvider({ children }: { children: ReactNode }) {
  const [cart, setCart] = useState<Cart | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const run = useCallback(async (operation: () => Promise<Cart>) => {
    setError(null);
    try {
      setCart(await operation());
    } catch (caughtError) {
      setError(getErrorMessage(caughtError, "A kosár művelet nem sikerült."));
      throw caughtError;
    }
  }, []);

  const refresh = useCallback(async () => {
    setIsLoading(true);
    try {
      await run(getCart);
    } finally {
      setIsLoading(false);
    }
  }, [run]);

  useEffect(() => {
    void refresh().catch(() => undefined);
  }, [refresh]);

  useEffect(() => {
    const handleAuthChange = () => {
      const operation = getAuthSession() ? mergeGuestCart : getCart;
      void run(operation).catch(() => undefined);
    };

    window.addEventListener(AUTH_CHANGED_EVENT, handleAuthChange);
    return () => window.removeEventListener(AUTH_CHANGED_EVENT, handleAuthChange);
  }, [run]);

  const value = useMemo<CartContextValue>(() => ({
    cart,
    isLoading,
    error,
    refresh,
    addItem: (productId, quantity = 1) => run(() => addCartItem(productId, quantity)),
    updateQuantity: (cartItemId, quantity) => run(() => updateCartItem(cartItemId, quantity)),
    removeItem: (cartItemId) => run(() => removeCartItem(cartItemId)),
    clear: () => run(clearCart),
  }), [cart, error, isLoading, refresh, run]);

  return <CartContext.Provider value={value}>{children}</CartContext.Provider>;
}
