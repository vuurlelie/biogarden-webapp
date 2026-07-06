import { Outlet } from "react-router-dom";
import Navbar from "../../shared/components/layout/Navbar";
import Footer from "../../shared/components/layout/Footer";
import ScrollToTop from "../router/ScrollToTop";
import BackToTopBtn from "../../shared/components/ui/BackToTopBtn";
import { useEffect, useState } from "react";
import { AUTH_CHANGED_EVENT, getAuthSession } from "../../features/auth/utils/authStorage";
import { useCart } from "../../features/cart/context/useCart";

export default function MainLayout() {
  const [auth, setAuth] = useState(getAuthSession);
  const { cart } = useCart();
  const isAuthenticated = auth !== null;
  const isAdmin = auth?.roles.some((role) => role.toLowerCase() === "admin") ?? false;
  const cartItemCount = cart?.totalItemCount ?? 0;

  useEffect(() => {
    const updateAuth = () => setAuth(getAuthSession());
    window.addEventListener(AUTH_CHANGED_EVENT, updateAuth);
    return () => window.removeEventListener(AUTH_CHANGED_EVENT, updateAuth);
  }, []);

  return (
    <div className="min-h-screen bg-white text-site-text">
        <ScrollToTop />

        <Navbar
        isAuthenticated={isAuthenticated}
        isAdmin={isAdmin}
        cartItemCount={cartItemCount}
        />

        <main className="flex-1">
        <Outlet />
        </main>

        <BackToTopBtn />
      
        <Footer />
    </div>
  );
}
