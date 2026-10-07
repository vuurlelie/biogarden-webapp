import { useEffect, useRef, useState, type FormEvent } from "react";
import { Link, useLocation, useNavigate } from "react-router";
import { login } from "../../features/auth/api/authApi";
import { getAuthSession, saveAuthSession } from "../../features/auth/utils/authStorage";
import { ROUTES } from "../../shared/constants/routes";

type AdminLoginLocationState = {
  from?: string;
};

type AdminLoginField = "email" | "password";
type AdminLoginFieldErrors = Partial<Record<AdminLoginField, string>>;

const inputClassName =
  "mt-2 block w-full rounded-sm border border-site-border bg-white px-3 py-2.5 text-site-heading outline-none transition focus:border-site-green focus:ring-2 focus:ring-site-green/20";
const invalidInputClassName =
  "mt-2 block w-full rounded-sm border border-site-danger bg-site-danger-soft px-3 py-2.5 text-site-heading outline-none transition focus:border-site-danger focus:ring-2 focus:ring-site-danger/20";

export default function AdminLoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const from = (location.state as AdminLoginLocationState | null)?.from ?? ROUTES.admin;
  const existingAuth = getAuthSession();
  const existingIsAdmin = existingAuth?.roles.some((role) => role.toLowerCase() === "admin") ?? false;
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState(existingAuth && !existingIsAdmin ? "A jelenlegi fiókod nem rendelkezik admin jogosultsággal." : "");
  const [fieldErrors, setFieldErrors] = useState<AdminLoginFieldErrors>({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const emailInputRef = useRef<HTMLInputElement | null>(null);
  const passwordInputRef = useRef<HTMLInputElement | null>(null);

  useEffect(() => {
    if (existingIsAdmin) {
      navigate(from, { replace: true });
    }
  }, [existingIsAdmin, from, navigate]);

  function clearFieldError(field: AdminLoginField) {
    setFieldErrors((currentErrors) => {
      if (!currentErrors[field]) return currentErrors;
      const nextErrors = { ...currentErrors };
      delete nextErrors[field];
      return nextErrors;
    });
  }

  function getErrorId(field: AdminLoginField) {
    return `admin-login-${field}-error`;
  }

  function getInputClassName(field: AdminLoginField) {
    return fieldErrors[field] ? invalidInputClassName : inputClassName;
  }

  function renderFieldError(field: AdminLoginField) {
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
    setError("");
    setFieldErrors({});

    if (!email.trim()) {
      setFieldErrors({ email: "Az email cím megadása kötelező." });
      emailInputRef.current?.focus();
      return;
    }

    if (!password) {
      setFieldErrors({ password: "A jelszó megadása kötelező." });
      passwordInputRef.current?.focus();
      return;
    }

    try {
      setIsSubmitting(true);
      const auth = await login({ email: email.trim(), password });
      saveAuthSession(auth);
      const isAdmin = auth.roles.some((role) => role.toLowerCase() === "admin");

      if (!isAdmin) {
        setError("Ezzel a fiókkal nem léphetsz be az admin felületre.");
        return;
      }

      navigate(from, { replace: true });
    } catch (submitError) {
      setError(
        submitError instanceof Error
          ? submitError.message
          : "Az admin bejelentkezés nem sikerült.",
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main id="main-content" tabIndex={-1} className="min-h-screen bg-site-surface-soft px-4 py-10 sm:py-14">
      <section className="mx-auto w-full max-w-md rounded-sm border border-site-border bg-white p-6 shadow-lg sm:p-8">
        <div className="text-center">
          <p className="text-xs font-bold uppercase tracking-[0.16em] text-site-green-dark">
            Adminisztráció
          </p>
          <h1 className="mt-2 text-3xl font-bold text-site-heading">Admin bejelentkezés</h1>
          <p className="mt-2 text-sm leading-6 text-site-text">
            Az admin felület használatához admin jogosultságú fiókkal kell belépned.
          </p>
        </div>

        {error && (
          <div className="mt-6 rounded-sm bg-site-danger-soft p-3 text-sm text-site-danger" role="alert">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} className="mt-6 space-y-5">
          <div>
            <label htmlFor="admin-login-email" className="block text-sm font-semibold text-site-heading">
              Email cím <span className="text-site-danger">*</span>
            </label>
            <input
              ref={emailInputRef}
              id="admin-login-email"
              type="email"
              autoComplete="email"
              value={email}
              onChange={(event) => {
                setEmail(event.target.value);
                clearFieldError("email");
              }}
              className={getInputClassName("email")}
              aria-invalid={Boolean(fieldErrors.email)}
              aria-describedby={fieldErrors.email ? getErrorId("email") : undefined}
              required
            />
            {renderFieldError("email")}
          </div>

          <div>
            <label htmlFor="admin-login-password" className="block text-sm font-semibold text-site-heading">
              Jelszó <span className="text-site-danger">*</span>
            </label>
            <input
              ref={passwordInputRef}
              id="admin-login-password"
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(event) => {
                setPassword(event.target.value);
                clearFieldError("password");
              }}
              className={getInputClassName("password")}
              aria-invalid={Boolean(fieldErrors.password)}
              aria-describedby={fieldErrors.password ? getErrorId("password") : undefined}
              required
            />
            {renderFieldError("password")}
          </div>

          <button
            type="submit"
            disabled={isSubmitting}
            className="w-full rounded-sm bg-site-green px-5 py-3 font-semibold text-white transition hover:bg-site-green-hover disabled:opacity-60"
          >
            {isSubmitting ? "Bejelentkezés..." : "Belépés az admin felületre"}
          </button>
        </form>

        <p className="mt-6 text-center text-sm text-site-text">
          Nem admin felületet keresel?{" "}
          <Link to={ROUTES.login} className="font-semibold text-site-green-dark hover:underline">
            Vásárlói bejelentkezés
          </Link>
        </p>
      </section>
    </main>
  );
}
