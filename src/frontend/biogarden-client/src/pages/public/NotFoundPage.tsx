import { Link } from "react-router-dom";

type NotFoundPageProps = {
  message?: string;
};

export default function NotFoundPage({
  message = "A keresett oldal nem található vagy már nem létezik.",
}: NotFoundPageProps) {
  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-100 px-4">
      <div className="max-w-lg w-full bg-white rounded-2xl shadow-xl p-8 text-center">
        <h1 className="text-7xl sm:text-8xl font-extrabold text-gray-300">
          404
        </h1>

        <h2 className="mt-4 text-2xl sm:text-3xl font-semibold text-gray-700">
          Oldal nem található
        </h2>

        <p className="mt-4 text-gray-500 text-sm sm:text-base">
          {message}
        </p>

        <div className="mt-8">
          <Link
            to="/"
            className="inline-flex items-center gap-2 bg-green-600 hover:bg-green-700 text-white px-5 py-2.5 rounded-lg transition-colors"
          >
            <span>Vissza a kezdőlapra</span>
          </Link>
        </div>
      </div>
    </div>
  );
}