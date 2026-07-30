import { useRef, useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router";
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
type RegisterFieldErrors = Partial<Record<RegisterInvalidField, string>>;

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
  const [formError, setFormError] = useState("");
  const [fieldErrors, setFieldErrors] = useState<RegisterFieldErrors>({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const emailInputRef = useRef<HTMLInputElement | null>(null);
  const passwordInputRef = useRef<HTMLInputElement | null>(null);
  const confirmPasswordInputRef = useRef<HTMLInputElement | null>(null);
  const companyNameInputRef = useRef<HTMLInputElement | null>(null);
  const taxNumberInputRef = useRef<HTMLInputElement | null>(null);
  const termsInputRef = useRef<HTMLInputElement | null>(null);

  const inputClassName =
    "mt-2 block w-full rounded-sm border border-site-border bg-white px-3 py-2.5 text-site-heading outline-none transition focus:border-site-green focus:ring-2 focus:ring-site-green/20";
  const invalidInputClassName =
    "mt-2 block w-full rounded-sm border border-site-danger bg-site-danger-soft px-3 py-2.5 text-site-heading outline-none transition focus:border-site-danger focus:ring-2 focus:ring-site-danger/20";

  function getInputClassName(field: RegisterInvalidField) {
    return fieldErrors[field] ? invalidInputClassName : inputClassName;
  }

  function getErrorId(field: RegisterInvalidField) {
    return `register-${field}-error`;
  }

  function renderFieldError(field: RegisterInvalidField) {
    if (!fieldErrors[field]) return null;

    return (
      <p id={getErrorId(field)} className="mt-1 text-xs font-semibold text-site-danger">
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

  function getServerField(message: string): RegisterInvalidField | null {
    const normalizedMessage = message.toLocaleLowerCase("hu-HU");
    if (normalizedMessage.includes("email")) return "email";
    if (normalizedMessage.includes("jelsz")) return "password";
    if (normalizedMessage.includes("cég") || normalizedMessage.includes("ceg")) return "companyName";
    if (normalizedMessage.includes("adó") || normalizedMessage.includes("ado")) return "taxNumber";
    if (normalizedMessage.includes("irányítószám") || normalizedMessage.includes("iranyitoszam")) {
      return "billingPostalCode";
    }
    if (normalizedMessage.includes("település") || normalizedMessage.includes("telepules")) return "billingCity";
    if (normalizedMessage.includes("közterület") || normalizedMessage.includes("kozterulet")) return "billingStreet";
    if (normalizedMessage.includes("házszám") || normalizedMessage.includes("hazszam")) return "billingHouseNumber";
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
      const message = getErrorMessage(submitError, "A regisztráció nem sikerült.");
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
    <main className="bg-site-surface-soft px-4 pb-10 pt-6 sm:pb-14 sm:pt-8">
      <section className="mx-auto w-full max-w-2xl rounded-sm border border-site-border bg-white p-6 shadow-lg sm:p-8">
        <div className="text-center">
          <p className="text-xs font-bold uppercase tracking-[0.16em] text-site-green-dark">Új vásárlói fiók</p>
          <h1 className="mt-2 text-3xl font-bold text-site-heading">Regisztráció</h1>
          <p className="mt-2 text-xs text-site-text">
            Hozz létre fiókot a gyorsabb rendeléshez és a korábbi rendelések követéséhez.
          </p>
        </div>

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
                    setFieldErrors({});
                  }}
                  className="mt-1 h-4 w-4 accent-site-green"
                />
                <span>
                  <strong className="block text-site-heading">Magánszemély</strong>
                </span>
              </label>
              <label className="flex cursor-pointer gap-3 rounded-sm border border-site-border p-3 text-sm text-site-text">
                <input
                  type="radio"
                  name="customerType"
                  checked={customerType === 1}
                  onChange={() => {
                    setCustomerType(1);
                    setFieldErrors({});
                  }}
                  className="mt-1 h-4 w-4 accent-site-green"
                />
                <span>
                  <strong className="block text-site-heading">Cég</strong>
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
                      clearFieldError("companyName");
                    }}
                    autoComplete="organization"
                    className={getInputClassName("companyName")}
                    aria-invalid={Boolean(fieldErrors.companyName)}
                    aria-describedby={fieldErrors.companyName ? getErrorId("companyName") : undefined}
                    required={customerType === 1}
                  />
                  {renderFieldError("companyName")}
                </label>
                <label className="text-sm font-semibold text-site-heading">
                  Adószám <span className="text-site-danger">*</span>
                  <input
                    ref={taxNumberInputRef}
                    value={taxNumber}
                    onChange={(event) => {
                      setTaxNumber(event.target.value);
                      clearFieldError("taxNumber");
                    }}
                    placeholder="12345678-1-12"
                    className={getInputClassName("taxNumber")}
                    aria-invalid={Boolean(fieldErrors.taxNumber)}
                    aria-describedby={fieldErrors.taxNumber ? getErrorId("taxNumber") : undefined}
                    required={customerType === 1}
                  />
                  {renderFieldError("taxNumber")}
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
                      setFieldErrors((current) => {
                        const next = { ...current };
                        delete next.billingPostalCode;
                        delete next.billingCity;
                        delete next.billingStreet;
                        delete next.billingHouseNumber;
                        return next;
                      });
                    }}
                    invalidFields={{
                      postalCode: Boolean(fieldErrors.billingPostalCode),
                      city: Boolean(fieldErrors.billingCity),
                      street: Boolean(fieldErrors.billingStreet),
                      houseNumber: Boolean(fieldErrors.billingHouseNumber),
                    }}
                    errorMessages={{
                      postalCode: fieldErrors.billingPostalCode,
                      city: fieldErrors.billingCity,
                      street: fieldErrors.billingStreet,
                      houseNumber: fieldErrors.billingHouseNumber,
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
              <label htmlFor="register-password" className="block text-sm font-semibold text-site-heading">Jelszó <span className="text-site-danger">*</span></label>
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
              <label htmlFor="register-password-again" className="block text-sm font-semibold text-site-heading">Jelszó újra <span className="text-site-danger">*</span></label>
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
              <Link to={ROUTES.terms} className="font-semibold text-site-green-dark hover:underline">
                Általános Szerződési Feltételeket
              </Link>{" "}
              és az{" "}
              <Link to={ROUTES.privacyPolicy} className="font-semibold text-site-green-dark hover:underline">
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
    </main>
  );
}
