import { useCallback } from "react";
import { useNavigate } from "react-router";
import { ROUTES } from "../../../shared/constants/routes";
import { getErrorMessage, hasErrorStatus } from "../../../shared/utils/errorHandling";

export function useAccountErrorHandler() {
  const navigate = useNavigate();

  return useCallback(
    (error: unknown, fallbackMessage: string): string | null => {
      if (hasErrorStatus(error, 401)) {
        navigate(
          ROUTES.login,
          { replace: true }
        );
        return null;
      }

      return getErrorMessage(error, fallbackMessage);
    },
    [navigate]
  );
}
