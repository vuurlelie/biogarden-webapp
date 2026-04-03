import { Outlet } from "react-router-dom";

export default function MainLayout() {
  return (
    <div className="min-h-screen flex flex-col">
      <header className="p-4 bg-green-600 text-white">
        Navbar (placeholder)
      </header>

      <main className="flex-1">
        <Outlet />
      </main>

      <footer className="p-4 bg-gray-800 text-white text-center">
        Footer (placeholder)
      </footer>
    </div>
  );
}