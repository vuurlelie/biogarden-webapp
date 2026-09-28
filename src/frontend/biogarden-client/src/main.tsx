import React from "react";
import ReactDOM from "react-dom/client";
import SessionRoutes from "./app/router/SessionRoutes";
import "./index.css";
import { CartProvider } from "./features/cart/context/CartContext";
import { AnalyticsProvider } from "./features/analytics/context/AnalyticsContext";
import { startAuthSessionSync } from "./features/auth/utils/authStorage";

const stopAuthSync = startAuthSessionSync();
if (import.meta.hot) import.meta.hot.dispose(stopAuthSync);

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <AnalyticsProvider>
      <CartProvider>
        <SessionRoutes />
      </CartProvider>
    </AnalyticsProvider>
  </React.StrictMode>
);
