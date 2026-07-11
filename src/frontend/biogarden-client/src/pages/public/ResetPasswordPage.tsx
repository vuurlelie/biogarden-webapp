import { useState, type FormEvent } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { resetPassword } from "../../features/auth/api/authApi";
import { ROUTES } from "../../shared/constants/routes";

const PASSWORD_PATTERN = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$/;

export default function ResetPasswordPage() {
  const [searchParams] = useSearchParams();
  const userId = searchParams.get("userId") ?? "";
  const token = searchParams.get("token") ?? "";
  const hasRequiredParams = Boolean(userId && token);
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [message, setMessage] = useState("");
  const [error, setError] = useState(
    hasRequiredParams ? "" : "A jelszó-visszaállító link hiányos vagy érvénytelen.",
  );
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setMessage("");
    setError("");

    if (!hasRequiredParams) {
      setError("A jelszó-visszaállító link hiányos vagy érvénytelen.");
      return;
    }

    if (!PASSWORD_PATTERN.test(password)) {
      setError("A jelszó legalább 8 karakteres legyen, kis- és nagybetűvel, valamint számmal.");
      return;
    }

    if (password !== confirmPassword) {
      setError("A két jelszó nem egyezik.");
      return;
    }

    try {
      setIsSubmitting(true);
      await resetPassword({
        userId,
        token,
        password,
        confirmPassword,
      });
      setPassword("");
      setConfirmPassword("");
      setMessage("Sikeresen beállítottad az új jelszavadat. Most már bejelentkezhetsz.");
    } catch (submitError) {
      setError(
        submitError instanceof Error
          ? submitError.message
          : "A jelszó visszaállítása nem sikerült.",
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="bg-site-surface-soft px-4 pb-10 pt-6 sm:pb-14 sm:pt-8">
      <section className="mx-auto w-full max-w-md rounded-sm border border-site-border bg-white p-6 shadow-lg sm:p-8">
        <div className="text-center">
          <p className="text-xs font-bold uppercase tracking-[0.16em] text-site-green-dark">
            Fiók helyreállítása
          </p>
          <h1 className="mt-2 text-3xl font-bold text-site-heading">Új jelszó beállítása</h1>
          <p className="mt-2 text-sm leading-6 text-site-text">
            Add meg az új jelszavadat a fiókodhoz.
          </p>
        </div>

        {message && (
          <div className="mt-6 rounded-sm border border-site-green/30 bg-site-surface-soft p-3 text-sm leading-6 text-site-green-dark" role="status">
            {message}
          </div>
        )}

        {error && (
          <div className="mt-6 rounded-sm bg-site-danger-soft p-3 text-sm leading-6 text-site-danger" role="alert">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} className="mt-6 space-y-5">
          <div>
            <label htmlFor="reset-password" className="block text-sm font-semibold text-site-heading">
              Új jelszó <span className="text-site-danger">*</span>
            </label>
            <input
              id="reset-password"
              type="password"
              autoComplete="new-password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              className="mt-2 block w-full rounded-sm border border-site-border bg-white px-3 py-2.5 text-site-heading outline-none transition focus:border-site-green focus:ring-2 focus:ring-site-green/20"
              required
              disabled={!hasRequiredParams || Boolean(message)}
            />
          </div>

          <div>
            <label htmlFor="reset-password-again" className="block text-sm font-semibold text-site-heading">
              Új jelszó újra <span className="text-site-danger">*</span>
            </label>
            <input
              id="reset-password-again"
              type="password"
              autoComplete="new-password"
              value={confirmPassword}
              onChange={(event) => setConfirmPassword(event.target.value)}
              className="mt-2 block w-full rounded-sm border border-site-border bg-white px-3 py-2.5 text-site-heading outline-none transition focus:border-site-green focus:ring-2 focus:ring-site-green/20"
              required
              disabled={!hasRequiredParams || Boolean(message)}
            />
          </div>

          <p className="text-xs leading-5 text-site-muted">
            Legalább 8 karakter, egy kisbetű, egy nagybetű és egy szám szükséges.
          </p>

          <button
            type="submit"
            disabled={isSubmitting || !hasRequiredParams || Boolean(message)}
            className="w-full rounded-sm bg-site-green px-5 py-3 font-semibold text-white transition hover:bg-site-green-hover disabled:cursor-not-allowed disabled:opacity-60"
          >
            {isSubmitting ? "Mentés..." : "Új jelszó mentése"}
          </button>
        </form>

        <p className="mt-6 text-center text-sm text-site-text">
          <Link to={ROUTES.login} className="font-semibold text-site-green-dark hover:underline">
            Vissza a bejelentkezéshez
          </Link>
        </p>
      </section>
    </main>
  );
}
