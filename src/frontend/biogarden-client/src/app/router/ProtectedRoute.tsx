import { Navigate, Outlet } from "react-router-dom";
import { ROUTES } from "../../shared/constants/routes";

export default function ProtectedRoute() {
  const isAuthenticated = true; // TODO: auth hook

  return isAuthenticated ? <Outlet /> : <Navigate to={ROUTES.login} replace />;
}