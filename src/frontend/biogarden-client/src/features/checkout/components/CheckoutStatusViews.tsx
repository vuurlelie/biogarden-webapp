import { Link } from "react-router";
import { ROUTES } from "../../../shared/constants/routes";

type CheckoutLoadingViewProps = {
  label?: string;
};

type CheckoutInterruptedViewProps = {
  error: string;
  submitting: boolean;
  onRetry: () => void;
};

export function CheckoutLoadingView({
  label = "A pénztár betöltése...",
}: CheckoutLoadingViewProps) {
  return (
    <div className="mx-auto max-w-7xl px-4 py-12 text-center">
      {label}
    </div>
  );
}

export function CheckoutInterruptedView({
  error,
  submitting,
  onRetry,
}: CheckoutInterruptedViewProps) {
  return (
    <main className="mx-auto max-w-2xl px-4 py-16 text-center">
      <h1 className="text-3xl font-bold text-site-heading">A fizetés megszakadt</h1>
      <p className="mt-4 text-site-text">
        A rendelést rögzítettük, de az online bankkártyás fizetés nem fejeződött be.
      </p>
      {error && (
        <div role="alert" className="mt-5 rounded-sm bg-site-danger-soft p-4 text-site-danger">
          {error}
        </div>
      )}
      <div className="mt-8 flex flex-col justify-center gap-3 sm:flex-row">
        <button
          type="button"
          onClick={onRetry}
          disabled={submitting}
          className="rounded-sm bg-site-green px-5 py-3 font-semibold text-white disabled:opacity-50"
        >
          {submitting ? "Átirányítás..." : "Fizetés újrapróbálása"}
        </button>
        <Link
          to={ROUTES.products}
          className="rounded-sm border border-site-border bg-white px-5 py-3 font-semibold text-site-heading"
        >
          Vissza a termékekhez
        </Link>
      </div>
    </main>
  );
}

export function CheckoutEmptyCartView() {
  return (
    <main className="mx-auto max-w-4xl px-4 py-12 text-center">
      <h1 className="text-3xl font-bold">A kosár üres</h1>
      <Link
        to={ROUTES.products}
        className="mt-6 inline-flex rounded-sm bg-site-green px-5 py-3 font-semibold text-white"
      >
        Vissza a termékekhez
      </Link>
    </main>
  );
}
