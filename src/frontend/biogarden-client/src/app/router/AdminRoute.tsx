import { Navigate, Outlet } from "react-router-dom";
import { ROUTES } from "../../shared/constants/routes";

export default function AdminRoute() {
  const isAuthenticated = true;
  const isAdmin = true;

  if (!isAuthenticated) {
    return <Navigate to={ROUTES.login} replace />;
  }

  return isAdmin ? <Outlet /> : <Navigate to={ROUTES.home} replace />;
}