import { useRef, useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { register } from "../../features/auth/api/authApi";
import AddressFields from "../../features/account/components/AddressFields";
import type { AddressFormData } from "../../features/account/types/address.types";
import { ROUTES } from "../../shared/constants/routes";
import { getErrorMessage } from "../../shared/utils/errorHandling";

const PASSWORD_PATTERN = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$/;
type RegisterInvalidField =
  | "email"
  | "password"
  | "confirmPassword"
  | "acceptedTerms"
  | "companyName"
  | "taxNumber"
  | "billingPostalCode"
  | "billingCity"
  | "billingStreet"
  | "billingHouseNumber";

const EMPTY_ADDRESS: AddressFormData = {
  postalCode: "",
  city: "",
  street: "",
  houseNumber: "",
  building: null,
  floor: null,
  door: null,
  doorbell: null,
};

function normalizeAddress(address: AddressFormData): AddressFormData {
  const normalizeOptional = (value: string | null) => value?.trim() || null;
  return {
    postalCode: address.postalCode.trim(),
    city: address.city.trim(),
    street: address.street.trim(),
    houseNumber: address.houseNumber.trim(),
    building: normalizeOptional(address.building),
    floor: normalizeOptional(address.floor),
    door: normalizeOptional(address.door),
    doorbell: normalizeOptional(address.doorbell),
  };
}

export default function RegisterPage() {
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [customerType, setCustomerType] = useState<0 | 1>(0);
  const [companyName, setCompanyName] = useState("");
  const [taxNumber, setTaxNumber] = useState("");
  const [billingAddress, setBillingAddress] = useState<AddressFormData>({ ...EMPTY_ADDRESS });
  const [printedInvoiceRequested, setPrintedInvoiceRequested] = useState(false);
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [acceptedTerms, setAcceptedTerms] = useState(false);
  const [error, setError] = useState("");
  const [invalidField, setInvalidField] = useState<RegisterInvalidField | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const emailInputRef = useRef<HTMLInputElement | null>(null);
  const passwordInputRef = useRef<HTMLInputElement | null>(null);
  const confirmPasswordInputRef = useRef<HTMLInputElement | null>(null);
  const companyNameInputRef = useRef<HTMLInputElement | null>(null);
  const taxNumberInputRef = useRef<HTMLInputElement | null>(null);
  const termsInputRef = useRef<HTMLInputElement | null>(null);

  function markInvalid(field: RegisterInvalidField, message: string) {
    setInvalidField(field);
    setError(message);
    const inputRefs: Partial<Record<RegisterInvalidField, HTMLElement | null>> = {
      email: emailInputRef.current,
      password: passwordInputRef.current,
      confirmPassword: confirmPasswordInputRef.current,
      acceptedTerms: termsInputRef.current,
      companyName: companyNameInputRef.current,
      taxNumber: taxNumberInputRef.current,
      billingPostalCode: document.getElementById("register-billing-postal-code"),
      billingCity: document.getElementById("register-billing-city"),
      billingStreet: document.getElementById("register-billing-street"),
      billingHouseNumber: document.getElementById("register-billing-house-number"),
    };
    inputRefs[field]?.focus();
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError("");
    setInvalidField(null);

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

    if (customerType === 1) {
      if (!companyName.trim()) {
        markInvalid("companyName", "Céges fiókhoz a cégnév megadása kötelező.");
        return;
      }

      if (!taxNumber.trim()) {
        markInvalid("taxNumber", "Céges fiókhoz az adószám megadása kötelező.");
        return;
      }

      if (!/^\d{8}(-\d-\d{2})?$/.test(taxNumber.trim())) {
        markInvalid("taxNumber", "Az adószám formátuma hibás. Példa: 12345678-1-12.");
        return;
      }

      if (!billingAddress.postalCode.trim()) {
        markInvalid("billingPostalCode", "Céges fiókhoz a számlázási irányítószám megadása kötelező.");
        return;
      }
      if (!billingAddress.city.trim()) {
        markInvalid("billingCity", "Céges fiókhoz a számlázási település megadása kötelező.");
        return;
      }
      if (!billingAddress.street.trim()) {
        markInvalid("billingStreet", "Céges fiókhoz a számlázási közterület megadása kötelező.");
        return;
      }
      if (!billingAddress.houseNumber.trim()) {
        markInvalid("billingHouseNumber", "Céges fiókhoz a számlázási házszám megadása kötelező.");
        return;
      }
    }

    try {
      setIsSubmitting(true);
      await register({
        email: email.trim(),
        password,
        confirmPassword,
        customerType,
        companyName: customerType === 1 ? companyName.trim() : null,
        taxNumber: customerType === 1 ? taxNumber.trim() : null,
        billingAddress: customerType === 1 ? normalizeAddress(billingAddress) : null,
        printedInvoiceRequested: customerType === 1 && printedInvoiceRequested,
      });
      navigate(ROUTES.login, {
        replace: true,
        state: { registrationRequiresConfirmation: true },
      });
    } catch (submitError) {
      setError(getErrorMessage(submitError, "A regisztráció nem sikerült."));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="bg-site-surface-soft px-4 pb-10 pt-6 sm:pb-14 sm:pt-8">
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

        <form onSubmit={handleSubmit} noValidate className="mt-6 space-y-5">
          <fieldset className="rounded-sm border border-site-border p-4">
            <legend className="px-2 text-sm font-bold text-site-heading">Fiók típusa</legend>
            <div className="grid gap-3 sm:grid-cols-2">
              <label className="flex cursor-pointer gap-3 rounded-sm border border-site-border p-3 text-sm text-site-text">
                <input
                  type="radio"
                  name="customerType"
                  checked={customerType === 0}
                  onChange={() => {
                    setCustomerType(0);
                    setPrintedInvoiceRequested(false);
                    setInvalidField(null);
                  }}
                  className="mt-1 h-4 w-4 accent-site-green"
                />
                <span>
                  <strong className="block text-site-heading">Magánszemély</strong>
                  Papíralapú nyugtát kapsz a rendelés mellé.
                </span>
              </label>
              <label className="flex cursor-pointer gap-3 rounded-sm border border-site-border p-3 text-sm text-site-text">
                <input
                  type="radio"
                  name="customerType"
                  checked={customerType === 1}
                  onChange={() => {
                    setCustomerType(1);
                    setInvalidField(null);
                  }}
                  className="mt-1 h-4 w-4 accent-site-green"
                />
                <span>
                  <strong className="block text-site-heading">Cég</strong>
                  Céges rendeléshez számlázási adatok szükségesek.
                </span>
              </label>
            </div>
          </fieldset>

          {customerType === 1 && (
            <fieldset className="rounded-sm border border-site-border p-4">
              <legend className="px-2 text-sm font-bold text-site-heading">Céges számlázási adatok</legend>
              <p className="mb-4 text-xs leading-5 text-site-muted">
                Alapértelmezetten elektronikus számlát küldünk a megadott email címre.
              </p>
              <div className="grid gap-5 sm:grid-cols-2">
                <label className="text-sm font-semibold text-site-heading">
                  Cégnév <span className="text-site-danger">*</span>
                  <input
                    ref={companyNameInputRef}
                    value={companyName}
                    onChange={(event) => {
                      setCompanyName(event.target.value);
                      if (invalidField === "companyName") setInvalidField(null);
                    }}
                    autoComplete="organization"
                    className="mt-2 block w-full rounded-sm border border-site-border px-3 py-2.5"
                    aria-invalid={invalidField === "companyName"}
                    required={customerType === 1}
                  />
                </label>
                <label className="text-sm font-semibold text-site-heading">
                  Adószám <span className="text-site-danger">*</span>
                  <input
                    ref={taxNumberInputRef}
                    value={taxNumber}
                    onChange={(event) => {
                      setTaxNumber(event.target.value);
                      if (invalidField === "taxNumber") setInvalidField(null);
                    }}
                    placeholder="12345678-1-12"
                    className="mt-2 block w-full rounded-sm border border-site-border px-3 py-2.5"
                    aria-invalid={invalidField === "taxNumber"}
                    required={customerType === 1}
                  />
                </label>
              </div>
              <div className="mt-5">
                <h2 className="text-sm font-bold text-site-heading">Számlázási cím</h2>
                <div className="mt-3">
                  <AddressFields
                    idPrefix="register-billing"
                    value={billingAddress}
                    onChange={(nextAddress) => {
                      setBillingAddress(nextAddress);
                      if (invalidField?.startsWith("billing")) setInvalidField(null);
                    }}
                    invalidFields={{
                      postalCode: invalidField === "billingPostalCode",
                      city: invalidField === "billingCity",
                      street: invalidField === "billingStreet",
                      houseNumber: invalidField === "billingHouseNumber",
                    }}
                  />
                </div>
              </div>
              <label className="mt-4 flex items-start gap-3 rounded-sm bg-site-surface-soft p-3 text-sm text-site-text">
                <input
                  type="checkbox"
                  checked={printedInvoiceRequested}
                  onChange={(event) => setPrintedInvoiceRequested(event.target.checked)}
                  className="mt-1 h-4 w-4 accent-site-green"
                />
                <span>Nyomtatott számlát is kérek a rendelés mellé.</span>
              </label>
            </fieldset>
          )}

          <div>
            <label htmlFor="register-email" className="block text-sm font-semibold text-site-heading">Email cím <span className="text-site-danger">*</span></label>
            <input
              ref={emailInputRef}
              id="register-email"
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

          <div className="grid gap-5 sm:grid-cols-2">
            <div>
              <label htmlFor="register-password" className="block text-sm font-semibold text-site-heading">Jelszó <span className="text-site-danger">*</span></label>
              <input
                ref={passwordInputRef}
                id="register-password"
                type="password"
                autoComplete="new-password"
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
            <div>
              <label htmlFor="register-password-again" className="block text-sm font-semibold text-site-heading">Jelszó újra <span className="text-site-danger">*</span></label>
              <input
                ref={confirmPasswordInputRef}
                id="register-password-again"
                type="password"
                autoComplete="new-password"
                value={confirmPassword}
                onChange={(event) => {
                  setConfirmPassword(event.target.value);
                  if (invalidField === "confirmPassword") setInvalidField(null);
                }}
                className="mt-2 block w-full rounded-sm border border-site-border bg-white px-3 py-2.5 text-site-heading outline-none transition focus:border-site-green focus:ring-2 focus:ring-site-green/20"
                aria-invalid={invalidField === "confirmPassword"}
                required
              />
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
                if (invalidField === "acceptedTerms") setInvalidField(null);
              }}
              className="mt-1 h-4 w-4 accent-site-green"
              aria-invalid={invalidField === "acceptedTerms"}
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
