import { useRef, useState, type FormEvent } from "react";
import { sendContactMessage } from "../api/contactApi";
import { useAnalytics } from "../../analytics/context/AnalyticsContextCore";
import { getErrorMessage } from "../../../shared/utils/errorHandling";

const initialForm = {
  name: "",
  email: "",
  phone: "",
  subject: "",
  message: "",
};

type ContactField = keyof typeof initialForm;

type ContactFormProps = {
  className?: string;
  surfaceClassName?: string;
  titleId?: string;
};

export default function ContactForm({
  className = "",
  surfaceClassName = "bg-white/65",
  titleId,
}: ContactFormProps) {
  const [form, setForm] = useState(initialForm);
  const [success, setSuccess] = useState("");
  const [error, setError] = useState("");
  const [invalidField, setInvalidField] = useState<ContactField | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const nameInputRef = useRef<HTMLInputElement | null>(null);
  const emailInputRef = useRef<HTMLInputElement | null>(null);
  const messageInputRef = useRef<HTMLTextAreaElement | null>(null);
  const { track } = useAnalytics();

  function updateField(field: keyof typeof form, value: string) {
    setForm((currentForm) => ({ ...currentForm, [field]: value }));
    if (invalidField === field) setInvalidField(null);
  }

  function markInvalid(field: ContactField, message: string) {
    setInvalidField(field);
    setError(message);
    const inputRefs: Partial<Record<ContactField, HTMLInputElement | HTMLTextAreaElement | null>> = {
      name: nameInputRef.current,
      email: emailInputRef.current,
      message: messageInputRef.current,
    };
    inputRefs[field]?.focus();
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSuccess("");
    setError("");
    setInvalidField(null);

    if (!form.name.trim()) {
      markInvalid("name", "Add meg a nevedet.");
      return;
    }

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email.trim())) {
      markInvalid("email", "Adj meg egy érvényes email címet.");
      return;
    }

    if (form.message.trim().length < 10) {
      markInvalid("message", "Kérjük, írj legalább néhány mondatot, hogy segíteni tudjunk.");
      return;
    }

    try {
      setIsSubmitting(true);
      await sendContactMessage({
        name: form.name.trim(),
        email: form.email.trim(),
        phone: form.phone.trim() || undefined,
        subject: form.subject.trim() || undefined,
        message: form.message.trim(),
      });
      track("contact_form_submitted", { source: "footer" });
      setSuccess("Köszönjük, megkaptuk az üzenetedet. Hamarosan válaszolunk.");
      setForm(initialForm);
    } catch (submitError) {
      setError(getErrorMessage(submitError, "Az üzenet küldése nem sikerült. Kérjük, próbáld újra."));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <form
      onSubmit={handleSubmit}
      noValidate
      className={`rounded-sm border border-site-footer-border ${surfaceClassName} p-4 shadow-sm sm:p-5 ${className}`}
    >
      <h2 id={titleId} className="text-sm font-bold uppercase tracking-wider text-site-heading">
        Írj nekünk
      </h2>

      <div className="mt-4 grid gap-3 sm:grid-cols-2">
        <div>
          <label htmlFor="contact-name" className="block text-sm font-semibold text-site-heading">
            Név <span className="text-site-danger">*</span>
          </label>
          <input
            ref={nameInputRef}
            id="contact-name"
            type="text"
            autoComplete="name"
            value={form.name}
            onChange={(event) => updateField("name", event.target.value)}
            className="mt-1.5 block w-full rounded-sm border border-site-border bg-white px-3 py-2 text-site-heading outline-none transition focus:border-site-green focus:ring-2 focus:ring-site-green/20"
            aria-invalid={invalidField === "name"}
            required
            maxLength={100}
          />
        </div>

        <div>
          <label htmlFor="contact-email" className="block text-sm font-semibold text-site-heading">
            Email cím <span className="text-site-danger">*</span>
          </label>
          <input
            ref={emailInputRef}
            id="contact-email"
            type="email"
            autoComplete="email"
            value={form.email}
            onChange={(event) => updateField("email", event.target.value)}
            className="mt-1.5 block w-full rounded-sm border border-site-border bg-white px-3 py-2 text-site-heading outline-none transition focus:border-site-green focus:ring-2 focus:ring-site-green/20"
            aria-invalid={invalidField === "email"}
            required
            maxLength={256}
          />
        </div>

        <div>
          <label htmlFor="contact-phone" className="block text-sm font-semibold text-site-heading">
            Telefonszám
          </label>
          <input
            id="contact-phone"
            type="tel"
            autoComplete="tel"
            value={form.phone}
            onChange={(event) => updateField("phone", event.target.value)}
            className="mt-1.5 block w-full rounded-sm border border-site-border bg-white px-3 py-2 text-site-heading outline-none transition focus:border-site-green focus:ring-2 focus:ring-site-green/20"
            maxLength={40}
          />
        </div>

        <div>
          <label htmlFor="contact-subject" className="block text-sm font-semibold text-site-heading">
            Tárgy
          </label>
          <input
            id="contact-subject"
            type="text"
            value={form.subject}
            onChange={(event) => updateField("subject", event.target.value)}
            className="mt-1.5 block w-full rounded-sm border border-site-border bg-white px-3 py-2 text-site-heading outline-none transition focus:border-site-green focus:ring-2 focus:ring-site-green/20"
            maxLength={150}
          />
        </div>
      </div>

      <div className="mt-3">
        <label htmlFor="contact-message" className="block text-sm font-semibold text-site-heading">
          Üzenet <span className="text-site-danger">*</span>
        </label>
        <textarea
          ref={messageInputRef}
          id="contact-message"
          value={form.message}
          onChange={(event) => updateField("message", event.target.value)}
          className="mt-1.5 block min-h-32 w-full resize-y rounded-sm border border-site-border bg-white px-3 py-2 text-site-heading outline-none transition focus:border-site-green focus:ring-2 focus:ring-site-green/20"
          aria-invalid={invalidField === "message"}
          required
          minLength={10}
          maxLength={2000}
        />
      </div>

      {success && (
        <div className="mt-4 rounded-sm border border-site-green/30 bg-site-surface-soft p-3 text-sm leading-6 text-site-green-dark" role="status">
          {success}
        </div>
      )}

      {error && (
        <div className="mt-4 rounded-sm bg-site-danger-soft p-3 text-sm text-site-danger" role="alert">
          {error}
        </div>
      )}

      <button
        type="submit"
        disabled={isSubmitting}
        className="mt-4 w-full rounded-sm bg-site-green px-5 py-3 font-semibold text-white transition hover:bg-site-green-hover disabled:cursor-not-allowed disabled:opacity-60 sm:w-auto"
      >
        {isSubmitting ? "Küldés..." : "Üzenet küldése"}
      </button>
    </form>
  );
}
