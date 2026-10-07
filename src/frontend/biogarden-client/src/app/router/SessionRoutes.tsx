import { RouterProvider } from "react-router";
import { router } from ".";
import { useAuthSession } from "../../features/auth/hooks/useAuthSession";

export default function SessionRoutes() {
  const auth = useAuthSession();
  // Discard mounted account/checkout state when the authenticated identity changes.
  return <RouterProvider key={auth?.sessionId ?? "guest"} router={router} />;
}
