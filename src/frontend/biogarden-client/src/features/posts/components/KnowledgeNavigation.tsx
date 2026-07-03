import { NavLink } from "react-router-dom";
import { ROUTES } from "../../../shared/constants/routes";

export default function KnowledgeNavigation() {
  return (
    <div className="px-4 pt-2 md:px-8 lg:px-0">
      <div className="mx-auto max-w-6xl text-sm text-site-text">
        <NavLink to={ROUTES.home} className="font-bold text-site-green hover:underline">
          Kezdőlap
        </NavLink>
        <span className="mx-2">/</span>
        <span className="font-bold text-site-green">Tudástár</span>
      </div>
    </div>
  );
}
