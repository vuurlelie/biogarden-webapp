import React, { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { CartProvider } from "../src/features/cart/context/CartContext";
import { useCart } from "../src/features/cart/context/useCart";
import { saveAuthSession } from "../src/features/auth/utils/authStorage";

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
      saveAuthSession({ userId: "customer", email: "customer@example.com", roles: [],
        sessionId: "customer-session", expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString() });
    }}>Sign in</button>
    <output data-testid="count">{cart.cart?.totalItemCount ?? "none"}</output>
    <output data-testid="loading">{String(cart.isLoading)}</output>
    <output data-testid="error">{cart.error ?? ""}</output>
  </>;
}

createRoot(document.getElementById("root")!).render(
  <StrictMode><CartProvider><CartControls /></CartProvider></StrictMode>,
);
