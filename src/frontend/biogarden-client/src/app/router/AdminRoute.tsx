import { Navigate, Outlet, useLocation } from "react-router-dom";
import { getAuthSession } from "../../features/auth/utils/authStorage";
import { ROUTES } from "../../shared/constants/routes";

export default function AdminRoute() {
  const location = useLocation();
  const auth = getAuthSession();

  if (!auth) {
    return (
      <Navigate
        to={ROUTES.login}
        replace
        state={{ from: `${location.pathname}${location.search}${location.hash}` }}
      />
    );
  }

  const isAdmin = auth.roles.some((role) => role.toLowerCase() === "admin");
  return isAdmin ? <Outlet /> : <Navigate to={ROUTES.home} replace />;
}
