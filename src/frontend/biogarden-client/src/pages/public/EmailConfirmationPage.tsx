import { useEffect, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router";
import { confirmEmail } from "../../features/auth/api/authApi";
import { ROUTES } from "../../shared/constants/routes";

type ConfirmationState = "loading" | "success" | "error";

export default function EmailConfirmationPage() {
  const [searchParams] = useSearchParams();
  const userId = searchParams.get("userId");
  const token = searchParams.get("token");
  const hasRequiredParams = Boolean(userId && token);
  const hasSubmittedRef = useRef(false);
  const [confirmationState, setConfirmationState] = useState<ConfirmationState>(
    hasRequiredParams ? "loading" : "error",
  );
  const [message, setMessage] = useState(
    hasRequiredParams
      ? "Email cím megerősítése folyamatban..."
      : "A megerősítő link hiányos vagy érvénytelen.",
  );

  useEffect(() => {
    if (!hasRequiredParams || !userId || !token) {
      return;
    }

    if (hasSubmittedRef.current) {
      return;
    }
    hasSubmittedRef.current = true;

    const confirmationUserId = userId;
    const confirmationToken = token;

    async function submitConfirmation() {
      try {
        await confirmEmail({
          userId: confirmationUserId,
          token: confirmationToken,
        });
        setConfirmationState("success");
        setMessage("Sikeresen megerősítetted az email címedet. Most már bejelentkezhetsz.");
      } catch (error) {
        setConfirmationState("error");
        setMessage(
          error instanceof Error
            ? error.message
            : "Az email cím megerősítése nem sikerült.",
        );
      }
    }

    submitConfirmation();
  }, [hasRequiredParams, token, userId]);

  const isSuccess = confirmationState === "success";
  const isLoading = confirmationState === "loading";

  return (
    <div className="bg-site-surface-soft px-4 py-10 sm:py-14">
      <section className="mx-auto w-full max-w-lg rounded-sm border border-site-border bg-white p-6 text-center shadow-lg sm:p-8">
        <p className="text-xs font-bold uppercase tracking-[0.16em] text-site-green-dark">
          Fiók aktiválása
        </p>
        <h1 className="mt-2 text-3xl font-bold text-site-heading">Email megerősítés</h1>

        <div
          className={`mt-6 rounded-sm p-4 text-sm leading-6 ${
            isSuccess || isLoading
              ? "border border-site-green/30 bg-site-surface-soft text-site-green-dark"
              : "bg-site-danger-soft text-site-danger"
          }`}
          role={isSuccess ? "status" : "alert"}
        >
          {message}
        </div>

        <div className="mt-6 flex flex-col gap-3 sm:flex-row sm:justify-center">
          <Link
            to={ROUTES.login}
            className="rounded-sm bg-site-green px-5 py-3 font-semibold text-white transition hover:bg-site-green-hover"
          >
            Bejelentkezés
          </Link>
          <Link
            to={ROUTES.home}
            className="rounded-sm border border-site-border px-5 py-3 font-semibold text-site-heading transition hover:border-site-green hover:text-site-green-dark"
          >
            Vissza a főoldalra
          </Link>
        </div>
      </section>
    </div>
  );
}
