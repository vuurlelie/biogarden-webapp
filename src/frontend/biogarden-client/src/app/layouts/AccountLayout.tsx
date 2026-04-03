import { Outlet } from "react-router-dom";
import ScrollToTop from "../router/ScrollToTop";

export default function AccountLayout() {
  return (
    <div className="min-h-screen flex">
        <ScrollToTop />
        
        <aside className="w-64 bg-gray-200 p-4">
        Account menu
        </aside>

        <main className="flex-1 p-6">
        <Outlet />
        </main>
    </div>
  );
}