import { Navigate, Outlet } from "react-router";
import { isAuthenticated } from "../../features/auth/utils/authStorage";
import { ROUTES } from "../../shared/constants/routes";

export default function GuestOnlyRoute() {
  return isAuthenticated() ? <Navigate to={ROUTES.account} replace /> : <Outlet />;
}
