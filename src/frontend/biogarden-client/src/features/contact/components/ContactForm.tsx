import { useState, type FormEvent } from "react";
import { sendContactMessage } from "../api/contactApi";

const initialForm = {
  name: "",
  email: "",
  phone: "",
  subject: "",
  message: "",
};

export default function ContactForm() {
  const [form, setForm] = useState(initialForm);
  const [success, setSuccess] = useState("");
  const [error, setError] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  function updateField(field: keyof typeof form, value: string) {
    setForm((currentForm) => ({ ...currentForm, [field]: value }));
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSuccess("");
    setError("");

    if (form.message.trim().length < 10) {
      setError("Kérjük, írj legalább néhány mondatot, hogy segíteni tudjunk.");
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
      setSuccess("Köszönjük, megkaptuk az üzenetedet. Hamarosan válaszolunk.");
      setForm(initialForm);
    } catch (submitError) {
      setError(
        submitError instanceof Error
          ? submitError.message
          : "Az üzenet küldése nem sikerült. Kérjük, próbáld újra.",
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <form
      onSubmit={handleSubmit}
      className="h-full rounded-sm border border-site-footer-border bg-white/65 p-4 shadow-sm sm:p-5"
    >
      <h2 className="text-sm font-bold uppercase tracking-wider text-site-heading">
        Írj nekünk
      </h2>

      <div className="mt-4 grid gap-3 sm:grid-cols-2">
        <div>
          <label htmlFor="contact-name" className="block text-sm font-semibold text-site-heading">
            Név <span className="text-site-danger">*</span>
          </label>
          <input
            id="contact-name"
            type="text"
            autoComplete="name"
            value={form.name}
            onChange={(event) => updateField("name", event.target.value)}
            className="mt-1.5 block w-full rounded-sm border border-site-border bg-white px-3 py-2 text-site-heading outline-none transition focus:border-site-green focus:ring-2 focus:ring-site-green/20"
            required
            maxLength={100}
          />
        </div>

        <div>
          <label htmlFor="contact-email" className="block text-sm font-semibold text-site-heading">
            Email cím <span className="text-site-danger">*</span>
          </label>
          <input
            id="contact-email"
            type="email"
            autoComplete="email"
            value={form.email}
            onChange={(event) => updateField("email", event.target.value)}
            className="mt-1.5 block w-full rounded-sm border border-site-border bg-white px-3 py-2 text-site-heading outline-none transition focus:border-site-green focus:ring-2 focus:ring-site-green/20"
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
          id="contact-message"
          value={form.message}
          onChange={(event) => updateField("message", event.target.value)}
          className="mt-1.5 block min-h-32 w-full resize-y rounded-sm border border-site-border bg-white px-3 py-2 text-site-heading outline-none transition focus:border-site-green focus:ring-2 focus:ring-site-green/20"
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
