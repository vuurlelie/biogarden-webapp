import { useRouteError } from "react-router";

export default function RouteErrorPage() {
  const error = useRouteError();
  const message = error instanceof Error ? error.message : "";
  const failedDownload = /Failed to fetch dynamically imported module|Importing a module script failed|Loading chunk|error loading dynamically imported module/i.test(message);

  return (
    <main className="mx-auto max-w-xl px-6 py-16 text-site-text">
      <h1 className="text-2xl font-bold text-site-heading">Az oldal betöltése nem sikerült</h1>
      <p role="alert" className="mt-4">
        {failedDownload
          ? "Az oldal egy részét nem sikerült letölteni. Ellenőrizd az internetkapcsolatot, majd töltsd újra az oldalt."
          : "Váratlan hiba történt. Próbáld meg újratölteni az oldalt."}
      </p>
      <p className="mt-3 text-sm">A nem mentett módosítások újratöltéskor elveszhetnek.</p>
      <button type="button" onClick={() => window.location.reload()} className="mt-6 rounded-sm bg-site-green px-5 py-3 font-semibold text-white">
        Oldal újratöltése
      </button>
      <a href="/" className="ml-4 underline">Vissza a kezdőlapra</a>
    </main>
  );
}
