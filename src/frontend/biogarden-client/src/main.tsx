import React from "react";
import ReactDOM from "react-dom/client";
import { RouterProvider } from "react-router";
import { router } from "./app/router";
import "./index.css";
import { CartProvider } from "./features/cart/context/CartContext";
import { AnalyticsProvider } from "./features/analytics/context/AnalyticsContext";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <AnalyticsProvider>
      <CartProvider>
        <RouterProvider router={router} />
      </CartProvider>
    </AnalyticsProvider>
  </React.StrictMode>
);
