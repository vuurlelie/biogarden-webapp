import { useRef, useState, type FormEvent } from "react";
import { Link, useSearchParams } from "react-router";
import { resetPassword } from "../../features/auth/api/authApi";
import { ROUTES } from "../../shared/constants/routes";
import { getErrorMessage, getServerFieldErrors } from "../../shared/utils/errorHandling";

const PASSWORD_PATTERN = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$/;
type PasswordField = "password" | "confirmPassword";
type PasswordFieldErrors = Partial<Record<PasswordField, string>>;

const inputClassName =
  "mt-2 block w-full rounded-sm border border-site-border bg-white px-3 py-2.5 text-site-heading outline-none transition focus:border-site-green focus:ring-2 focus:ring-site-green/20";
const invalidInputClassName =
  "mt-2 block w-full rounded-sm border border-site-danger bg-site-danger-soft px-3 py-2.5 text-site-heading outline-none transition focus:border-site-danger focus:ring-2 focus:ring-site-danger/20";

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
  const [fieldErrors, setFieldErrors] = useState<PasswordFieldErrors>({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const passwordInputRef = useRef<HTMLInputElement | null>(null);
  const confirmPasswordInputRef = useRef<HTMLInputElement | null>(null);

  function markInvalid(field: PasswordField, message: string) {
    setFieldErrors({ [field]: message });
    setError("");
    const inputRefs: Record<PasswordField, HTMLInputElement | null> = {
      password: passwordInputRef.current,
      confirmPassword: confirmPasswordInputRef.current,
    };
    inputRefs[field]?.focus();
  }

  function clearFieldError(field: PasswordField) {
    setFieldErrors((currentErrors) => {
      if (!currentErrors[field]) return currentErrors;
      const nextErrors = { ...currentErrors };
      delete nextErrors[field];
      return nextErrors;
    });
  }

  function getErrorId(field: PasswordField) {
    return `reset-${field}-error`;
  }

  function getInputClassName(field: PasswordField) {
    return fieldErrors[field] ? invalidInputClassName : inputClassName;
  }

  function renderFieldError(field: PasswordField) {
    const fieldError = fieldErrors[field];

    if (!fieldError) return null;

    return (
      <p id={getErrorId(field)} className="mt-1.5 text-sm text-site-danger" role="alert">
        {fieldError}
      </p>
    );
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setMessage("");
    setError("");
    setFieldErrors({});

    if (!hasRequiredParams) {
      setError("A jelszó-visszaállító link hiányos vagy érvénytelen.");
      return;
    }

    if (!PASSWORD_PATTERN.test(password)) {
      markInvalid("password", "A jelszó legalább 8 karakteres legyen, kis- és nagybetűvel, valamint számmal.");
      return;
    }

    if (password !== confirmPassword) {
      markInvalid("confirmPassword", "A két jelszó nem egyezik.");
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
      setFieldErrors({});
      setMessage("Sikeresen beállítottad az új jelszavadat. Most már bejelentkezhetsz.");
    } catch (submitError) {
      setFieldErrors(getServerFieldErrors(submitError, ["password", "confirmPassword"] as const));
      setError(getErrorMessage(submitError, "A jelszó visszaállítása nem sikerült."));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="bg-site-surface-soft px-4 pb-10 pt-6 sm:pb-14 sm:pt-8">
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

        <form onSubmit={handleSubmit} noValidate className="mt-6 space-y-5">
          <div>
            <label htmlFor="reset-password" className="block text-sm font-semibold text-site-heading">
              Új jelszó <span className="text-site-danger">*</span>
            </label>
            <input
              ref={passwordInputRef}
              id="reset-password"
              type="password"
              autoComplete="new-password"
              value={password}
              onChange={(event) => {
                setPassword(event.target.value);
                clearFieldError("password");
              }}
              className={getInputClassName("password")}
              aria-invalid={Boolean(fieldErrors.password)}
              aria-describedby={fieldErrors.password ? getErrorId("password") : undefined}
              required
              disabled={!hasRequiredParams || Boolean(message)}
            />
            {renderFieldError("password")}
          </div>

          <div>
            <label htmlFor="reset-password-again" className="block text-sm font-semibold text-site-heading">
              Új jelszó újra <span className="text-site-danger">*</span>
            </label>
            <input
              ref={confirmPasswordInputRef}
              id="reset-password-again"
              type="password"
              autoComplete="new-password"
              value={confirmPassword}
              onChange={(event) => {
                setConfirmPassword(event.target.value);
                clearFieldError("confirmPassword");
              }}
              className={getInputClassName("confirmPassword")}
              aria-invalid={Boolean(fieldErrors.confirmPassword)}
              aria-describedby={fieldErrors.confirmPassword ? getErrorId("confirmPassword") : undefined}
              required
              disabled={!hasRequiredParams || Boolean(message)}
            />
            {renderFieldError("confirmPassword")}
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
    </div>
  );
}
