import { useRef, useState, type FormEvent } from "react";
import { Link } from "react-router";
import { forgotPassword } from "../../features/auth/api/authApi";
import { ROUTES } from "../../shared/constants/routes";
import { getErrorMessage } from "../../shared/utils/errorHandling";

export default function ForgotPasswordPage() {
  const [email, setEmail] = useState("");
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const [emailError, setEmailError] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const emailInputRef = useRef<HTMLInputElement | null>(null);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setMessage("");
    setError("");
    setEmailError("");

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) {
      setEmailError("Adj meg egy érvényes email címet.");
      emailInputRef.current?.focus();
      return;
    }

    try {
      setIsSubmitting(true);
      await forgotPassword({ email: email.trim() });
      setMessage(
        "Ha létezik megfelelő fiók ezzel az email címmel, feldolgozzuk a jelszó-visszaállítási kérést. Az email később is érkezhet; nézd meg a spam és promóciók mappát is.",
      );
    } catch (submitError) {
      setError(getErrorMessage(submitError, "A jelszó-visszaállító email küldése nem sikerült."));
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
          <h1 className="mt-2 text-3xl font-bold text-site-heading">Elfelejtett jelszó</h1>
          <p className="mt-2 text-sm leading-6 text-site-text">
            Add meg a fiókodhoz tartozó email címet, és küldünk egy jelszó-visszaállító linket.
          </p>
        </div>

        {message && (
          <div className="mt-6 rounded-sm border border-site-green/30 bg-site-surface-soft p-3 text-sm leading-6 text-site-green-dark" role="status">
            {message}
          </div>
        )}

        {error && (
          <div className="mt-6 rounded-sm bg-site-danger-soft p-3 text-sm text-site-danger" role="alert">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} noValidate className="mt-6 space-y-5">
          <div>
            <label htmlFor="forgot-password-email" className="block text-sm font-semibold text-site-heading">
              Email cím <span className="text-site-danger">*</span>
            </label>
            <input
              ref={emailInputRef}
              id="forgot-password-email"
              type="email"
              autoComplete="email"
              value={email}
              onChange={(event) => {
                setEmail(event.target.value);
                if (emailError) setEmailError("");
              }}
              className={`mt-2 block w-full rounded-sm border px-3 py-2.5 text-site-heading outline-none transition ${
                emailError
                  ? "border-site-danger bg-site-danger-soft focus:border-site-danger focus:ring-2 focus:ring-site-danger/20"
                  : "border-site-border bg-white focus:border-site-green focus:ring-2 focus:ring-site-green/20"
              }`}
              aria-invalid={Boolean(emailError)}
              aria-describedby={emailError ? "forgot-password-email-error" : undefined}
              required
            />
            {emailError && (
              <p id="forgot-password-email-error" className="mt-1.5 text-sm text-site-danger" role="alert">
                {emailError}
              </p>
            )}
          </div>

          <button
            type="submit"
            disabled={isSubmitting}
            className="w-full rounded-sm bg-site-green px-5 py-3 font-semibold text-white transition hover:bg-site-green-hover disabled:cursor-not-allowed disabled:opacity-60"
          >
            {isSubmitting ? "Küldés..." : "Jelszó-visszaállító link küldése"}
          </button>
        </form>

        <p className="mt-6 text-center text-sm text-site-text">
          Eszedbe jutott?{" "}
          <Link to={ROUTES.login} className="font-semibold text-site-green-dark hover:underline">
            Jelentkezz be!
          </Link>
        </p>
      </section>
    </main>
  );
}
