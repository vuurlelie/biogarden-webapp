import { useState } from "react";
import { requestAccountLifecycle } from "../../features/account/api/profileApi";
import AccountFeedback from "../../features/account/components/AccountFeedback";
import { useAccountErrorHandler } from "../../features/account/hooks/useAccountErrorHandler";
import type { AccountLifecycleRequestType } from "../../features/account/types/profile.types";
import { clearAuthSession } from "../../features/auth/utils/authStorage";
import { ROUTES } from "../../shared/constants/routes";
import { useNavigate } from "react-router-dom";

type ActionState = "idle" | "deactivation" | "erasure";

export default function AccountDeletePage() {
  const navigate = useNavigate();
  const handleAccountError = useAccountErrorHandler();
  const [reason, setReason] = useState("");
  const [saving, setSaving] = useState<ActionState>("idle");
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function submit(type: AccountLifecycleRequestType, state: ActionState) {
    setSaving(state);
    setMessage(null);
    setError(null);

    try {
      const result = await requestAccountLifecycle({
        type,
        reason: reason.trim() || null,
      });
      setMessage(result.message);
      if (result.sessionEnded) {
        clearAuthSession();
        window.setTimeout(() => navigate(ROUTES.home, { replace: true }), 1800);
      }
    } catch (submitError) {
      const message = handleAccountError(submitError, "A kérelem elküldése nem sikerült.");
      if (message === null) {
        clearAuthSession();
        return;
      }
      setError(message);
    } finally {
      setSaving("idle");
    }
  }

  return (
    <div className="space-y-6">
      <header>
        <p className="text-sm font-semibold uppercase tracking-[0.14em] text-site-green-dark">Fiók kezelése</p>
        <h1 className="mt-2 text-3xl font-bold text-site-heading">Deaktiválás vagy törlés</h1>
      </header>

      <AccountFeedback error={error ?? ""} success={message ?? ""} />

      <section className="rounded-sm border border-site-border bg-white p-5 shadow-sm">
        <label className="block text-sm font-semibold text-site-heading">
          Megjegyzés
          <textarea
            value={reason}
            onChange={(event) => setReason(event.target.value)}
            maxLength={1000}
            className="mt-2 min-h-28 w-full rounded-sm border border-site-border px-3 py-2.5"
          />
        </label>
      </section>

      <section className="grid gap-5 lg:grid-cols-2">
        <article className="rounded-sm border border-site-border bg-white p-5 shadow-sm">
          <h2 className="text-xl font-bold text-site-heading">Fiók deaktiválása</h2>
          <p className="mt-2 text-sm leading-6 text-site-text">
            A bejelentkezést kikapcsoljuk, de az adatok megmaradnak, ezért a fiók később újraaktiválható.
            Újraaktiválást a <a href="#contact" className="font-semibold text-site-green-dark underline-offset-4 hover:underline">Kapcsolat</a> részen keresztül kérhetsz.
          </p>
          <button
            type="button"
            onClick={() => void submit(1, "deactivation")}
            disabled={saving !== "idle"}
            className="mt-5 rounded-sm border border-site-green-dark px-4 py-2.5 text-sm font-semibold text-site-green-dark hover:bg-site-surface-soft disabled:opacity-60"
          >
            {saving === "deactivation" ? "Küldés..." : "Deaktiválást kérek"}
          </button>
        </article>

        <article className="rounded-sm border border-site-danger/30 bg-white p-5 shadow-sm">
          <h2 className="text-xl font-bold text-site-heading">Személyes adatok törlése</h2>
          <p className="mt-2 text-sm leading-6 text-site-text">
            Ha nincs rendelési előzmény, a fiókot töröljük. Rendelési előzmény esetén a mentett fiókadatokat anonimizáljuk, a korábbi rendelésekhez tartozó rendelési és számlázási adatokat pedig megőrizzük, mert ezekre számviteli, adózási, garanciális vagy jogi igények kezelése miatt szükség lehet.
          </p>
          <button
            type="button"
            onClick={() => void submit(2, "erasure")}
            disabled={saving !== "idle"}
            className="mt-5 rounded-sm bg-site-danger px-4 py-2.5 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-60"
          >
            {saving === "erasure" ? "Küldés..." : "Törlést kérek"}
          </button>
        </article>
      </section>
    </div>
  );
}
