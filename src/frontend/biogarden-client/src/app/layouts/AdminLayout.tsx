import { Outlet } from "react-router-dom";
import ScrollToTop from "../router/ScrollToTop";

export default function AdminLayout() {
  return (
    <div className="min-h-screen flex">
        <ScrollToTop />

        <aside className="w-64 bg-gray-900 text-white p-4">
        Admin menu
        </aside>

        <main className="flex-1 p-6 bg-gray-100">
        <Outlet />
        </main>
    </div>
  );
}