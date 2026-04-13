import { NavLink } from "react-router-dom";
import { ROUTES } from "../../../shared/constants/routes";

export default function KnowledgeNavigation() {
  return (
    <div className="px-4 pt-2 md:px-8 lg:px-0">
      <div className="mx-auto max-w-6xl text-sm text-gray-700">
        <NavLink to={ROUTES.home} className="font-bold text-[#6F8625] hover:underline">
          Kezdőlap
        </NavLink>
        <span className="mx-2">/</span>
        <span className="font-bold text-[#6F8625]">Tudástár</span>
      </div>
    </div>
  );
}