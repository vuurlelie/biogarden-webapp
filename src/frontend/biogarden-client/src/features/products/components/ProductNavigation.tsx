import { NavLink } from "react-router";
import { ROUTES } from "../../../shared/constants/routes";

export default function ProductNavigation() {
  return (
    <nav className="text-sm text-site-text" aria-label="Morzsamenü">
      <NavLink
        to={ROUTES.home}
        className="font-bold text-site-green hover:underline"
      >
        Kezdőlap
      </NavLink>
      <span className="px-2">/</span>
      <span className="font-bold text-site-green">Termékeink</span>
    </nav>
  );
}
