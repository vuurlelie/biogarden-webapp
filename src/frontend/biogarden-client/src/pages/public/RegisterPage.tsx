import { useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { register } from "../../features/auth/api/authApi";
import { ROUTES } from "../../shared/constants/routes";

const PASSWORD_PATTERN = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$/;

export default function RegisterPage() {
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [acceptedTerms, setAcceptedTerms] = useState(false);
  const [error, setError] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError("");

    if (!PASSWORD_PATTERN.test(password)) {
      setError("A jelszó legalább 8 karakteres legyen, kis- és nagybetűvel, valamint számmal.");
      return;
    }

    if (password !== confirmPassword) {
      setError("A két jelszó nem egyezik.");
      return;
    }

    if (!acceptedTerms) {
      setError("A regisztrációhoz el kell fogadnod a feltételeket és az adatkezelési tájékoztatót.");
      return;
    }

    try {
      setIsSubmitting(true);
      await register({ email: email.trim(), password, confirmPassword });
      navigate(ROUTES.login, {
        replace: true,
        state: { registrationSuccess: true },
      });
    } catch (submitError) {
      setError(
        submitError instanceof Error
          ? submitError.message
          : "A regisztráció nem sikerült.",
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="bg-site-surface-soft px-4 py-10 sm:py-14">
      <section className="mx-auto w-full max-w-lg rounded-sm border border-site-border bg-white p-6 shadow-lg sm:p-8">
        <div className="text-center">
          <p className="text-xs font-bold uppercase tracking-[0.16em] text-site-green-dark">Új vásárlói fiók</p>
          <h1 className="mt-2 text-3xl font-bold text-site-heading">Regisztráció</h1>
          <p className="mt-2 text-sm text-site-text">
            Hozz létre fiókot a gyorsabb rendeléshez és a korábbi rendelések követéséhez.
          </p>
        </div>

        {error && (
          <div className="mt-6 rounded-sm bg-site-danger-soft p-3 text-sm text-site-danger" role="alert">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} className="mt-6 space-y-5">
          <div>
            <label htmlFor="register-email" className="block text-sm font-semibold text-site-heading">Email cím <span className="text-site-danger">*</span></label>
            <input
              id="register-email"
              type="email"
              autoComplete="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              className="mt-2 block w-full rounded-sm border border-site-border bg-white px-3 py-2.5 text-site-heading outline-none transition focus:border-site-green focus:ring-2 focus:ring-site-green/20"
              required
            />
          </div>

          <div className="grid gap-5 sm:grid-cols-2">
            <div>
              <label htmlFor="register-password" className="block text-sm font-semibold text-site-heading">Jelszó <span className="text-site-danger">*</span></label>
              <input
                id="register-password"
                type="password"
                autoComplete="new-password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                className="mt-2 block w-full rounded-sm border border-site-border bg-white px-3 py-2.5 text-site-heading outline-none transition focus:border-site-green focus:ring-2 focus:ring-site-green/20"
                required
              />
            </div>
            <div>
              <label htmlFor="register-password-again" className="block text-sm font-semibold text-site-heading">Jelszó újra <span className="text-site-danger">*</span></label>
              <input
                id="register-password-again"
                type="password"
                autoComplete="new-password"
                value={confirmPassword}
                onChange={(event) => setConfirmPassword(event.target.value)}
                className="mt-2 block w-full rounded-sm border border-site-border bg-white px-3 py-2.5 text-site-heading outline-none transition focus:border-site-green focus:ring-2 focus:ring-site-green/20"
                required
              />
            </div>
          </div>

          <p className="text-xs leading-5 text-site-muted">
            Legalább 8 karakter, egy kisbetű, egy nagybetű és egy szám szükséges.
          </p>

          <label className="flex items-start gap-3 rounded-sm bg-site-surface-soft p-4 text-sm leading-6 text-site-text">
            <input
              type="checkbox"
              checked={acceptedTerms}
              onChange={(event) => setAcceptedTerms(event.target.checked)}
              className="mt-1 h-4 w-4 accent-site-green"
            />
            <span>
              Elolvastam és elfogadom az{" "}
              <Link to={ROUTES.terms} className="font-semibold text-site-green-dark hover:underline">
                Általános Szerződési Feltételeket
              </Link>{" "}
              és az{" "}
              <Link to={ROUTES.privacyPolicy} className="font-semibold text-site-green-dark hover:underline">
                Adatkezelési tájékoztatót
              </Link>.
            </span>
          </label>

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
    </main>
  );
}
