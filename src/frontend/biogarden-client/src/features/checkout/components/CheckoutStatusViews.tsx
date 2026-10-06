import { Link } from "react-router";
import { ROUTES } from "../../../shared/constants/routes";

type CheckoutLoadingViewProps = {
  label?: string;
};

export function CheckoutLoadingView({
  label = "A pénztár betöltése...",
}: CheckoutLoadingViewProps) {
  return (
    <div className="mx-auto max-w-6xl px-4 py-12 text-center">
      {label}
    </div>
  );
}

export function CheckoutEmptyCartView() {
  return (
    <div className="mx-auto max-w-4xl px-4 py-12 text-center">
      <h1 className="text-3xl font-bold">A kosár üres</h1>
      <Link
        to={ROUTES.products}
        className="mt-6 inline-flex rounded-sm bg-site-green px-5 py-3 font-semibold text-white"
      >
        Vissza a termékekhez
      </Link>
    </div>
  );
}
