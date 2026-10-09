import React from "react";
import ReactDOM from "react-dom/client";
import SessionRoutes from "./app/router/SessionRoutes";
import "./index.css";
import { CartProvider } from "./features/cart/context/CartContext";
import { AnalyticsProvider } from "./features/analytics/context/AnalyticsContext";
import { refreshAuthSession, startAuthSessionSync } from "./features/auth/utils/authStorage";

const stopAuthSync = startAuthSessionSync();
if (import.meta.hot)
  import.meta.hot.dispose(stopAuthSync);

const root = ReactDOM.createRoot(document.getElementById("root")!);
async function start() {
  root.render(<main><p role="status">A webhely betöltése…</p></main>);
  try {
    await refreshAuthSession(true);
  } catch {
    root.render(
      <main>
        <p>A webhely betöltése nem sikerült.</p>
        <button onClick={() => void start()}>Újrapróbálás</button>
      </main>,
    );
    return;
  }
  root.render(
    <React.StrictMode>
      <AnalyticsProvider>
        <CartProvider>
          <SessionRoutes />
        </CartProvider>
      </AnalyticsProvider>
    </React.StrictMode>,
  );
}
void start();
