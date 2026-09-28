import React, { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { CartProvider } from "../src/features/cart/context/CartContext";
import { useCart } from "../src/features/cart/context/useCart";
import { AUTH_CHANGED_EVENT } from "../src/features/auth/utils/authStorage";

// Test-only consumer: exercises overlapping calls that normal screen loading states
// may hide. Uses the production provider and HTTP client, without production hooks.
export function CartControls() {
  const cart = useCart();
  const ignore = (operation: Promise<void>) => void operation.catch(() => undefined);
  return <>
    <button onClick={() => ignore(cart.addItem(1))}>Add first</button>
    <button onClick={() => ignore(cart.addItem(2))}>Add second</button>
    <button onClick={() => ignore(cart.refresh())}>Refresh</button>
    <button onClick={() => {
      const payload = btoa(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + 3600 }));
      localStorage.setItem("biogarden_auth", JSON.stringify({
        userId: "customer", email: "customer@example.com", roles: [], token: `test.${payload}.token`,
      }));
      window.dispatchEvent(new Event(AUTH_CHANGED_EVENT));
    }}>Sign in</button>
    <output data-testid="count">{cart.cart?.totalItemCount ?? "none"}</output>
    <output data-testid="loading">{String(cart.isLoading)}</output>
    <output data-testid="error">{cart.error ?? ""}</output>
  </>;
}

createRoot(document.getElementById("root")!).render(
  <StrictMode><CartProvider><CartControls /></CartProvider></StrictMode>,
);
