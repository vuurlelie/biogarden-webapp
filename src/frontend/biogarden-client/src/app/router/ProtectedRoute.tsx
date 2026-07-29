import { Navigate, Outlet } from "react-router";
import { isAuthenticated } from "../../features/auth/utils/authStorage";
import { ROUTES } from "../../shared/constants/routes";

export default function ProtectedRoute() {
  return isAuthenticated() ? <Outlet /> : <Navigate to={ROUTES.login} replace />;
}
