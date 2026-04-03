import { Outlet } from "react-router-dom";
import Navbar from "../../shared/components/layout/Navbar";
import Footer from "../../shared/components/layout/Footer";
import ScrollToTop from "../router/ScrollToTop";
import BackToTopBtn from "../../shared/components/ui/BackToTopBtn";

export default function MainLayout() {
  const isAuthenticated = false;
  const isAdmin = false;
  const cartItemCount = 0;

  return (
    <div className="min-h-screen bg-[#fffdf8] text-gray-900">
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