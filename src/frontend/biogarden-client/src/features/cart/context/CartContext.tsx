import { useCallback, useEffect, useMemo, useRef, useState } from "react";
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
  const sessionGeneration = useRef(0);
  const pending = useRef<Promise<void>>(Promise.resolve());
  const sessionReady = useRef(false);
  const initializeSession = useRef<() => Promise<Cart>>(getCart);
  const started = useRef(false);
  const mounted = useRef(true);

  const run = useCallback((operation: (() => Promise<Cart>) | null) => {
    const generation = sessionGeneration.current;
    const sessionId = getAuthSession()?.sessionId;
    const isCurrent = () => mounted.current && generation === sessionGeneration.current && sessionId === getAuthSession()?.sessionId;
    const assertCurrent = () => {
      if (!isCurrent()) throw new DOMException("A kosár munkamenete megváltozott.", "AbortError");
    };
    const task = pending.current.then(async () => {
      assertCurrent();
      setError(null);
      const showLoading = operation === null || !sessionReady.current;
      if (showLoading) setIsLoading(true);
      try {
        let result: Cart | undefined;
        // Establish the guest cookie (or finish login merging) before any mutation.
        // Failed initialization stays retryable and must not allow the write through.
        if (!sessionReady.current) {
          result = await initializeSession.current();
          assertCurrent();
          sessionReady.current = true;
        }
        if (operation !== null || result === undefined) {
          result = await (operation ?? getCart)();
        }
        assertCurrent();
        setCart(result);
      } catch (caughtError) {
        if (isCurrent()) setError(getErrorMessage(caughtError, "A kosár művelet nem sikerült."));
        throw caughtError;
      } finally {
        if (showLoading && isCurrent()) setIsLoading(false);
      }
    });
    // A rejected operation must not poison subsequent work. Keep in-flight guest
    // writes ahead of login merging, but reject obsolete queued writes before sending.
    pending.current = task.catch(() => undefined);
    return task;
  }, []);

  const refresh = useCallback(() => run(null), [run]);

  useEffect(() => {
    mounted.current = true;
    const handleAuthChange = () => {
      ++sessionGeneration.current;
      sessionReady.current = false;
      initializeSession.current = getAuthSession() ? mergeGuestCart : getCart;
      setCart(null);
      setError(null);
      setIsLoading(true);
      void refresh().catch(() => undefined);
    };

    window.addEventListener(AUTH_CHANGED_EVENT, handleAuthChange);
    if (!started.current) {
      started.current = true;
      void refresh().catch(() => undefined);
    }
    return () => {
      mounted.current = false;
      window.removeEventListener(AUTH_CHANGED_EVENT, handleAuthChange);
    };
  }, [refresh]);

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
