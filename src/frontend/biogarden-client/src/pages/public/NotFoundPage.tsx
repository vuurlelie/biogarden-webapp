import Seo from "../../shared/seo/Seo";
import { Link } from "react-router";

type NotFoundPageProps = {
  message?: string;
};

export default function NotFoundPage({
  message = "A keresett oldal nem található vagy már nem létezik.",
}: NotFoundPageProps) {
  return (
    <div className="min-h-screen flex items-center justify-center bg-site-surface-soft px-4">
      <div className="max-w-lg w-full bg-white rounded-2xl shadow-xl p-8 text-center">
        <Seo title="Az oldal nem található" noIndex />
        <h1 className="text-7xl sm:text-8xl font-extrabold text-site-border">
          404
        </h1>

        <h2 className="mt-4 text-2xl sm:text-3xl font-semibold text-site-heading">
          Oldal nem található
        </h2>

        <p className="mt-4 text-site-text text-sm sm:text-base">
          {message}
        </p>

        <div className="mt-8">
          <Link
            to="/"
            className="inline-flex items-center gap-2 bg-site-green hover:bg-site-green-hover text-white px-5 py-2.5 rounded-lg transition-colors"
          >
            <span>Vissza a kezdőlapra</span>
          </Link>
        </div>
      </div>
    </div>
  );
}
