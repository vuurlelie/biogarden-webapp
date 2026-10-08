import { useRef, useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router";
import { register } from "../../features/auth/api/authApi";
import { ROUTES } from "../../shared/constants/routes";
import { getErrorMessage, getServerFieldErrors } from "../../shared/utils/errorHandling";

const PASSWORD_PATTERN = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$/;

type RegisterInvalidField =
  | "email"
  | "password"
  | "confirmPassword"
  | "acceptedTerms";

type RegisterFieldErrors = Partial<Record<RegisterInvalidField, string>>;

export default function RegisterPage() {
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [acceptedTerms, setAcceptedTerms] = useState(false);
  const [formError, setFormError] = useState("");
  const [fieldErrors, setFieldErrors] = useState<RegisterFieldErrors>({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const emailInputRef = useRef<HTMLInputElement | null>(null);
  const passwordInputRef = useRef<HTMLInputElement | null>(null);
  const confirmPasswordInputRef = useRef<HTMLInputElement | null>(null);
  const termsInputRef = useRef<HTMLInputElement | null>(null);

  const inputClassName =
    "mt-2 block w-full rounded-sm border border-site-border bg-white px-3 py-2.5 text-site-heading outline-none transition focus:border-gray-600";
  const invalidInputClassName =
    "mt-2 block w-full rounded-sm border border-site-danger bg-site-danger-soft px-3 py-2.5 text-site-heading outline-none transition focus:border-gray-600";

  function getInputClassName(field: RegisterInvalidField) {
    return fieldErrors[field] ? invalidInputClassName : inputClassName;
  }

  function getErrorId(field: RegisterInvalidField) {
    return `register-${field}-error`;
  }

  function renderFieldError(field: RegisterInvalidField) {
    if (!fieldErrors[field]) return null;

    return (
      <p id={getErrorId(field)} className="mt-1 text-xs font-normal text-site-danger">
        {fieldErrors[field]}
      </p>
    );
  }

  function clearFieldError(field: RegisterInvalidField) {
    setFieldErrors((current) => {
      if (!current[field]) return current;
      const next = { ...current };
      delete next[field];
      return next;
    });
  }

  function markInvalid(field: RegisterInvalidField, message: string) {
    setFormError("");
    setFieldErrors({ [field]: message });
    const inputRefs: Record<RegisterInvalidField, HTMLElement | null> = {
      email: emailInputRef.current,
      password: passwordInputRef.current,
      confirmPassword: confirmPasswordInputRef.current,
      acceptedTerms: termsInputRef.current,
    };
    inputRefs[field]?.focus();
  }

  function getServerField(message: string): RegisterInvalidField | null {
    const normalizedMessage = message.toLocaleLowerCase("hu-HU");
    if (normalizedMessage.includes("email")) return "email";
    if (normalizedMessage.includes("jelsz")) return "password";
    return null;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError("");
    setFieldErrors({});

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) {
      markInvalid("email", "Adj meg egy érvényes email címet.");
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

    if (!acceptedTerms) {
      markInvalid("acceptedTerms", "A regisztrációhoz el kell fogadnod a feltételeket és az adatkezelési tájékoztatót.");
      return;
    }

    try {
      setIsSubmitting(true);
      await register({
        email: email.trim(),
        password,
        confirmPassword,
        customerType: 0,
        companyName: null,
        taxNumber: null,
        billingAddress: null,
        printedInvoiceRequested: false,
      });
      navigate(ROUTES.login, {
        replace: true,
        state: { registrationRequiresConfirmation: true },
      });
    } catch (submitError) {
      const message = getErrorMessage(submitError, "A regisztráció nem sikerült.");
      const structuredErrors = getServerFieldErrors(submitError, ["email", "password", "confirmPassword"] as const);
      if (Object.keys(structuredErrors).length) {
        setFieldErrors(structuredErrors);
        setFormError(message);
        return;
      }
      const serverField = getServerField(message);
      if (serverField) {
        markInvalid(serverField, message);
      } else {
        setFormError(message);
      }
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="bg-site-surface-soft px-4 pb-10 pt-6 sm:pb-14 sm:pt-8">
      <section className="mx-auto w-full max-w-xl rounded-sm border border-site-border bg-white p-6 shadow-lg sm:p-8">
        <div className="text-center">
          <p className="text-xs font-bold uppercase tracking-[0.16em] text-site-green-dark">Új vásárlói fiók</p>
          <h1 className="mt-2 text-3xl font-bold text-site-heading">Regisztráció</h1>
          <p className="mt-2 text-xs text-site-text">
            Hozz létre fiókot a gyorsabb rendeléshez és a korábbi rendelések követéséhez.
          </p>
        </div>

        <form onSubmit={handleSubmit} noValidate className="mt-6 space-y-5">
          <div>
            <label htmlFor="register-email" className="block text-sm font-semibold text-site-heading">
              Email cím <span className="text-site-danger">*</span>
            </label>
            <input
              ref={emailInputRef}
              id="register-email"
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

          <div className="grid gap-5 sm:grid-cols-2">
            <div>
              <label htmlFor="register-password" className="block text-sm font-semibold text-site-heading">
                Jelszó <span className="text-site-danger">*</span>
              </label>
              <input
                ref={passwordInputRef}
                id="register-password"
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
              />
              {renderFieldError("password")}
            </div>
            <div>
              <label htmlFor="register-password-again" className="block text-sm font-semibold text-site-heading">
                Jelszó újra <span className="text-site-danger">*</span>
              </label>
              <input
                ref={confirmPasswordInputRef}
                id="register-password-again"
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
              />
              {renderFieldError("confirmPassword")}
            </div>
          </div>

          <p className="text-xs leading-5 text-site-muted">
            Legalább 8 karakter, egy kisbetű, egy nagybetű és egy szám szükséges.
          </p>

          <label className="flex items-start gap-3 rounded-sm bg-site-surface-soft p-4 text-sm leading-6 text-site-text">
            <input
              ref={termsInputRef}
              type="checkbox"
              checked={acceptedTerms}
              onChange={(event) => {
                setAcceptedTerms(event.target.checked);
                clearFieldError("acceptedTerms");
              }}
              className="mt-1 h-4 w-4 accent-site-green"
              aria-invalid={Boolean(fieldErrors.acceptedTerms)}
              aria-describedby={fieldErrors.acceptedTerms ? getErrorId("acceptedTerms") : undefined}
            />
            <span>
              Elolvastam és elfogadom az{" "}
              <Link
                to={ROUTES.terms}
                target="_blank"
                rel="noopener noreferrer"
                className="font-semibold text-site-green-dark hover:underline"
              >
                Általános Szerződési Feltételeket
              </Link>{" "}
              és az{" "}
              <Link
                to={ROUTES.privacyPolicy}
                target="_blank"
                rel="noopener noreferrer"
                className="font-semibold text-site-green-dark hover:underline"
              >
                Adatkezelési tájékoztatót
              </Link>.
            </span>
          </label>
          {renderFieldError("acceptedTerms")}

          {formError && (
            <div className="rounded-sm bg-site-danger-soft p-3 text-sm text-site-danger" role="alert">
              {formError}
            </div>
          )}

          <button
            type="submit"
            disabled={isSubmitting}
            className="w-full rounded-sm bg-site-green px-5 py-3 font-semibold text-white transition hover:bg-site-green-hover disabled:cursor-not-allowed disabled:opacity-60"
          >
            {isSubmitting ? "Regisztráció..." : "Regisztráció"}
          </button>
        </form>

        <p className="mt-6 text-center text-sm text-site-text">
          Már van fiókod?{" "}
          <Link to={ROUTES.login} className="font-semibold text-site-green-dark hover:underline">
            Jelentkezz be!
          </Link>
        </p>
      </section>
    </div>
  );
}
