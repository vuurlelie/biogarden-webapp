import { useRef, useState, type FormEvent } from "react";
import { requestPublicOrderWithdrawal } from "../../features/account/api/ordersApi";
import { useAnalytics } from "../../features/analytics/context/AnalyticsContextCore";
import Seo from "../../shared/seo/Seo";
import { getErrorMessage } from "../../shared/utils/errorHandling";

type WithdrawalField = "orderNumber" | "customerEmail" | "reason";
type WithdrawalFieldErrors = Partial<Record<WithdrawalField, string>>;

const fieldClassName =
  "mt-2 block w-full rounded-sm border border-site-border bg-white px-3 py-2.5 text-site-heading outline-none transition focus:border-site-green focus:ring-2 focus:ring-site-green/20";
const invalidFieldClassName =
  "mt-2 block w-full rounded-sm border border-site-danger bg-site-danger-soft px-3 py-2.5 text-site-heading outline-none transition focus:border-site-danger focus:ring-2 focus:ring-site-danger/20";

export default function OrderWithdrawalRequestPage() {
  const [orderNumber, setOrderNumber] = useState("");
  const [customerEmail, setCustomerEmail] = useState("");
  const [reason, setReason] = useState("");
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const [fieldErrors, setFieldErrors] = useState<WithdrawalFieldErrors>({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const orderNumberInputRef = useRef<HTMLInputElement | null>(null);
  const emailInputRef = useRef<HTMLInputElement | null>(null);
  const reasonInputRef = useRef<HTMLTextAreaElement | null>(null);
  const { track } = useAnalytics();

  function markInvalid(field: WithdrawalField, messageText: string) {
    setFieldErrors({ [field]: messageText });
    setError("");
    const inputRefs: Record<WithdrawalField, HTMLInputElement | HTMLTextAreaElement | null> = {
      orderNumber: orderNumberInputRef.current,
      customerEmail: emailInputRef.current,
      reason: reasonInputRef.current,
    };
    inputRefs[field]?.focus();
  }

  function clearFieldError(field: WithdrawalField) {
    setFieldErrors((currentErrors) => {
      if (!currentErrors[field]) return currentErrors;
      const nextErrors = { ...currentErrors };
      delete nextErrors[field];
      return nextErrors;
    });
  }

  function getErrorId(field: WithdrawalField) {
    return `withdrawal-${field}-error`;
  }

  function getFieldClassName(field: WithdrawalField) {
    return fieldErrors[field] ? invalidFieldClassName : fieldClassName;
  }

  function renderFieldError(field: WithdrawalField) {
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

    if (!orderNumber.trim()) {
      markInvalid("orderNumber", "Add meg a rendelés azonosítóját.");
      return;
    }

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(customerEmail.trim())) {
      markInvalid("customerEmail", "Adj meg egy érvényes email címet.");
      return;
    }

    if (reason.trim().length < 10) {
      markInvalid("reason", "Írd le legalább 10 karakterben a kérésedet.");
      return;
    }

    try {
      setIsSubmitting(true);
      await requestPublicOrderWithdrawal({
        orderNumber,
        customerEmail,
        reason,
      });
      track("withdrawal_request_submitted", { source: "public_form" });
      setReason("");
      setFieldErrors({});
      setMessage("A kérésedet rögzítettük, és emailben is visszaigazoltuk.");
    } catch (submitError) {
      const errorMessage = getErrorMessage(
        submitError,
        "A kérés elküldése nem sikerült. Kérjük, ellenőrizd az adatokat és próbáld újra.",
      );
      setFieldErrors({
        orderNumber: errorMessage,
        customerEmail: errorMessage,
      });
      setError("");
      orderNumberInputRef.current?.focus();
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="bg-site-surface-soft px-4 pb-12 pt-8 sm:pb-16">
      <Seo
        title="Elállási vagy fogyasztói kérés"
        description="Rendeléshez kapcsolódó elállási, panasz- vagy fogyasztói kérés rögzítése a Bakony Kincse Biokertészetnél."
      />
      <section className="mx-auto w-full max-w-2xl rounded-sm border border-site-border bg-white p-6 shadow-lg sm:p-8">
        <div className="text-center">
          <p className="text-xs font-bold uppercase tracking-[0.16em] text-site-green-dark">
            Rendeléshez kapcsolódó ügyintézés
          </p>
          <h1 className="mt-2 text-3xl font-bold text-site-heading">Elállási vagy fogyasztói kérés</h1>
          <p className="mt-3 text-sm leading-6 text-site-text">
            Itt jelezheted, ha a rendeléseddel kapcsolatban elállást, panaszt vagy más fogyasztói kérést szeretnél
            rögzíteni. A kérés beérkezését emailben visszaigazoljuk, a feldolgozásáról külön egyeztetünk.
          </p>
        </div>

        {message && (
          <div className="mt-6 rounded-sm border border-site-green/30 bg-site-surface-soft p-4 text-sm leading-6 text-site-green-dark" role="status">
            {message}
          </div>
        )}

        {error && (
          <div className="mt-6 rounded-sm bg-site-danger-soft p-4 text-sm leading-6 text-site-danger" role="alert">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} noValidate className="mt-7 space-y-5">
          <div>
            <label htmlFor="withdrawal-order-number" className="block text-sm font-semibold text-site-heading">
              Rendelés azonosítója <span className="text-site-danger">*</span>
            </label>
            <input
              ref={orderNumberInputRef}
              id="withdrawal-order-number"
              type="text"
              value={orderNumber}
              onChange={(event) => {
                setOrderNumber(event.target.value);
                clearFieldError("orderNumber");
              }}
              placeholder="BK-..."
              className={getFieldClassName("orderNumber")}
              aria-invalid={Boolean(fieldErrors.orderNumber)}
              aria-describedby={fieldErrors.orderNumber ? getErrorId("orderNumber") : undefined}
              required
            />
            {renderFieldError("orderNumber")}
          </div>

          <div>
            <label htmlFor="withdrawal-email" className="block text-sm font-semibold text-site-heading">
              A rendelésnél megadott email cím <span className="text-site-danger">*</span>
            </label>
            <input
              ref={emailInputRef}
              id="withdrawal-email"
              type="email"
              autoComplete="email"
              value={customerEmail}
              onChange={(event) => {
                setCustomerEmail(event.target.value);
                clearFieldError("customerEmail");
              }}
              className={getFieldClassName("customerEmail")}
              aria-invalid={Boolean(fieldErrors.customerEmail)}
              aria-describedby={fieldErrors.customerEmail ? getErrorId("customerEmail") : undefined}
              required
            />
            {renderFieldError("customerEmail")}
          </div>

          <div>
            <label htmlFor="withdrawal-reason" className="block text-sm font-semibold text-site-heading">
              Kérés leírása <span className="text-site-danger">*</span>
            </label>
            <textarea
              ref={reasonInputRef}
              id="withdrawal-reason"
              value={reason}
              onChange={(event) => {
                setReason(event.target.value);
                clearFieldError("reason");
              }}
              maxLength={1000}
              rows={5}
              className={getFieldClassName("reason")}
              aria-invalid={Boolean(fieldErrors.reason)}
              aria-describedby={fieldErrors.reason ? getErrorId("reason") : undefined}
              required
            />
            {renderFieldError("reason")}
            <p className="mt-1 text-xs text-site-muted">Legfeljebb 1000 karakter.</p>
          </div>

          <button
            type="submit"
            disabled={isSubmitting}
            className="w-full rounded-sm bg-site-green px-5 py-3 font-semibold text-white transition hover:bg-site-green-hover disabled:cursor-not-allowed disabled:opacity-60"
          >
            {isSubmitting ? "Küldés..." : "Kérés elküldése"}
          </button>
        </form>
      </section>
    </div>
  );
}
