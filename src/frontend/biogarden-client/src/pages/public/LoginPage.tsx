import { useRef, useState, type FormEvent } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { login, resendEmailConfirmation } from "../../features/auth/api/authApi";
import { saveAuthSession } from "../../features/auth/utils/authStorage";
import { ROUTES } from "../../shared/constants/routes";
import { getErrorMessage } from "../../shared/utils/errorHandling";

type LoginLocationState = {
  registrationSuccess?: boolean;
  registrationRequiresConfirmation?: boolean;
  from?: string;
};

type LoginField = "email" | "password";

export default function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const registrationSuccess = Boolean(
    (location.state as LoginLocationState | null)?.registrationSuccess,
  );
  const registrationRequiresConfirmation = Boolean(
    (location.state as LoginLocationState | null)?.registrationRequiresConfirmation,
  );
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [infoMessage, setInfoMessage] = useState("");
  const [invalidField, setInvalidField] = useState<LoginField | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isResendingConfirmation, setIsResendingConfirmation] = useState(false);
  const [showResendConfirmation, setShowResendConfirmation] = useState(registrationRequiresConfirmation);
  const emailInputRef = useRef<HTMLInputElement | null>(null);
  const passwordInputRef = useRef<HTMLInputElement | null>(null);

  function markInvalid(field: LoginField, message: string) {
    setInvalidField(field);
    setError(message);
    const inputRefs: Record<LoginField, HTMLInputElement | null> = {
      email: emailInputRef.current,
      password: passwordInputRef.current,
    };
    inputRefs[field]?.focus();
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError("");
    setInfoMessage("");
    setInvalidField(null);

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) {
      markInvalid("email", "Adj meg egy érvényes email címet.");
      return;
    }

    if (!password) {
      markInvalid("password", "A jelszó megadása kötelező.");
      return;
    }

    try {
      setIsSubmitting(true);
      const auth = await login({ email: email.trim(), password });
      saveAuthSession(auth);
      const from = (location.state as LoginLocationState | null)?.from;
      navigate(from ?? ROUTES.account, { replace: true });
    } catch (submitError) {
      const message = getErrorMessage(submitError, "A bejelentkezés nem sikerült.");

      if (message.toLocaleLowerCase("hu-HU").includes("meger")) {
        setShowResendConfirmation(true);
      }

      setError(message);
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleResendConfirmation() {
    setError("");
    setInfoMessage("");
    setInvalidField(null);

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) {
      markInvalid("email", "Add meg az email címedet az új megerősítő email küldéséhez.");
      return;
    }

    try {
      setIsResendingConfirmation(true);
      await resendEmailConfirmation({ email: email.trim() });
      setInfoMessage("Ha a fiók még megerősítésre vár, elküldtük az új megerősítő emailt.");
    } catch (resendError) {
      setError(getErrorMessage(resendError, "A megerősítő email újraküldése nem sikerült."));
    } finally {
      setIsResendingConfirmation(false);
    }
  }

  return (
    <main className="bg-site-surface-soft px-4 pb-10 pt-6 sm:pb-14 sm:pt-8">
      <section className="mx-auto w-full max-w-md rounded-sm border border-site-border bg-white p-6 shadow-lg sm:p-8">
        <div className="text-center">
          <p className="text-xs font-bold uppercase tracking-[0.16em] text-site-green-dark">
            Vásárlói fiók
          </p>
          <h1 className="mt-2 text-3xl font-bold text-site-heading">Bejelentkezés</h1>
          <p className="mt-2 text-sm text-site-text">
            Lépj be a fiókod és rendeléseid kezeléséhez.
          </p>
        </div>

        {registrationSuccess && (
          <div className="mt-6 rounded-sm border border-site-green/30 bg-site-surface-soft p-3 text-sm text-site-green-dark" role="status">
            Sikeres regisztráció. Most már bejelentkezhetsz.
          </div>
        )}

        {registrationRequiresConfirmation && (
          <div className="mt-6 rounded-sm border border-site-green/30 bg-site-surface-soft p-3 text-sm leading-6 text-site-green-dark" role="status">
            Sikeres regisztráció. Elküldtük a megerősítő emailt, kérjük, kattints a benne található linkre a bejelentkezés előtt.
          </div>
        )}

        {infoMessage && (
          <div className="mt-6 rounded-sm border border-site-green/30 bg-site-surface-soft p-3 text-sm text-site-green-dark" role="status">
            {infoMessage}
          </div>
        )}

        {error && (
          <div className="mt-6 rounded-sm bg-site-danger-soft p-3 text-sm text-site-danger" role="alert">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} noValidate className="mt-6 space-y-5">
          <div>
            <label htmlFor="login-email" className="block text-sm font-semibold text-site-heading">
              Email cím <span className="text-site-danger">*</span>
            </label>
            <input
              ref={emailInputRef}
              id="login-email"
              type="email"
              autoComplete="email"
              value={email}
              onChange={(event) => {
                setEmail(event.target.value);
                if (invalidField === "email") setInvalidField(null);
              }}
              className="mt-2 block w-full rounded-sm border border-site-border bg-white px-3 py-2.5 text-site-heading outline-none transition focus:border-site-green focus:ring-2 focus:ring-site-green/20"
              aria-invalid={invalidField === "email"}
              required
            />
          </div>

          <div>
            <div className="flex items-center justify-between gap-3">
              <label htmlFor="login-password" className="block text-sm font-semibold text-site-heading">
                Jelszó <span className="text-site-danger">*</span>
              </label>
              <Link to={ROUTES.forgotPassword} className="text-sm font-semibold text-site-green-dark hover:underline">
                Elfelejtett jelszó?
              </Link>
            </div>
            <input
              ref={passwordInputRef}
              id="login-password"
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(event) => {
                setPassword(event.target.value);
                if (invalidField === "password") setInvalidField(null);
              }}
              className="mt-2 block w-full rounded-sm border border-site-border bg-white px-3 py-2.5 text-site-heading outline-none transition focus:border-site-green focus:ring-2 focus:ring-site-green/20"
              aria-invalid={invalidField === "password"}
              required
            />
          </div>

          <button
            type="submit"
            disabled={isSubmitting}
            className="w-full rounded-sm bg-site-green px-5 py-3 font-semibold text-white transition hover:bg-site-green-hover disabled:cursor-not-allowed disabled:opacity-60"
          >
            {isSubmitting ? "Bejelentkezés..." : "Bejelentkezés"}
          </button>
        </form>

        <p className="mt-6 text-center text-sm text-site-text">
          Még nincs fiókod?{" "}
          <Link to={ROUTES.register} className="font-semibold text-site-green-dark hover:underline">
            Regisztrálj!
          </Link>
        </p>

        {showResendConfirmation && (
          <div className="mt-4 text-center text-sm text-site-text">
            <button
              type="button"
              onClick={handleResendConfirmation}
              disabled={isResendingConfirmation}
              className="font-semibold text-site-green-dark underline-offset-4 hover:underline disabled:cursor-not-allowed disabled:opacity-60"
            >
              {isResendingConfirmation ? "Küldés..." : "Megerősítő email újraküldése"}
            </button>
          </div>
        )}
      </section>
    </main>
  );
}
