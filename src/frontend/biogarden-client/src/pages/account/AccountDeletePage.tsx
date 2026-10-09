import { useEffect, useState } from "react";
import { requestAccountLifecycle } from "../../features/account/api/profileApi";
import { getOrders } from "../../features/account/api/ordersApi";
import AccountFeedback from "../../features/account/components/AccountFeedback";
import { useAccountErrorHandler } from "../../features/account/hooks/useAccountErrorHandler";
import type { AccountLifecycleRequestType } from "../../features/account/types/profile.types";
import { clearAuthSession } from "../../features/auth/utils/authStorage";
import { ROUTES } from "../../shared/constants/routes";
import { useNavigate } from "react-router";

type ActionState = "idle" | "deactivation" | "erasure";
type ConfirmationAction = Exclude<ActionState, "idle">;

const confirmationCopy: Record<
  ConfirmationAction,
  {
    title: string;
    confirmLabel: string;
    buttonClassName: string;
  }
> = {
  deactivation: {
    title: "Biztosan deaktiválod a fiókodat?",
    confirmLabel: "Igen, deaktiválom",
    buttonClassName:
      "rounded-sm border border-site-green-dark bg-white px-4 py-2.5 text-sm font-semibold text-site-green-dark transition hover:bg-site-surface-soft",
  },
  erasure: {
    title: "Biztosan kéred a személyes adatok törlését?",
    confirmLabel: "Igen, törlést kérek",
    buttonClassName:
      "rounded-sm bg-site-danger px-4 py-2.5 text-sm font-semibold text-white transition hover:opacity-90",
  },
};

export default function AccountDeletePage() {
  const navigate = useNavigate();
  const handleAccountError = useAccountErrorHandler();
  const [reason, setReason] = useState("");
  const [saving, setSaving] = useState<ActionState>("idle");
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [confirmationAction, setConfirmationAction] = useState<ConfirmationAction | null>(null);
  const [hasOrderHistory, setHasOrderHistory] = useState<boolean | null>(null);

  useEffect(
    () => {
      let shouldIgnoreResult = false;

      async function loadOrderHistoryState() {
        try {
          const orders = await getOrders({ view: "all", pageSize: 1 });
          if (!shouldIgnoreResult)
            setHasOrderHistory(orders.totalCount > 0);
        } catch {
          if (!shouldIgnoreResult)
            setHasOrderHistory(null);
        }
      }

      void loadOrderHistoryState();
      return () => {
        shouldIgnoreResult = true;
      };
    },
    []
  );

  async function submit(type: AccountLifecycleRequestType, action: ConfirmationAction) {
    setSaving(action);
    setConfirmationAction(null);
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
        window.setTimeout(
          () => navigate(
            ROUTES.home,
            { replace: true }
          ),
          1800
        );
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
        <article className="flex flex-col rounded-sm border border-site-border bg-white p-5 shadow-sm">
          <h2 className="text-xl font-bold text-site-heading">Fiók deaktiválása</h2>
          <p className="mt-2 text-sm leading-6 text-site-text">
            A bejelentkezést kikapcsoljuk, de az adatok megmaradnak, ezért a fiók később újraaktiválható.
            Újraaktiválást a <a href="#contact" className="font-semibold text-site-green-dark underline-offset-4 hover:underline">Kapcsolat</a> részen keresztül kérhetsz.
          </p>
          <div className="mt-auto pt-8">
            <button
              type="button"
              onClick={() => setConfirmationAction("deactivation")}
              disabled={saving !== "idle"}
              className="w-fit rounded-sm border border-site-green-dark px-4 py-2.5 text-sm font-semibold text-site-green-dark hover:bg-site-surface-soft disabled:opacity-60"
            >
              {saving === "deactivation" ? "Küldés..." : "Deaktiválást kérek"}
            </button>
          </div>
        </article>

        <article className="flex flex-col rounded-sm border border-site-danger/30 bg-white p-5 shadow-sm">
          <h2 className="text-xl font-bold text-site-heading">Személyes adatok törlése</h2>
          <p className="mt-2 text-sm leading-6 text-site-text">
            {hasOrderHistory === null
              ? "A kérés elküldése előtt ellenőrizzük a rendelési előzményeket."
              : hasOrderHistory
                ? "A fiókodba mentett személyes adataidat anonimizáljuk. A korábbi rendeléseidhez tartozó rendelési és számlázási adatokat azonban megőrizzük a könyvelési és jogi kötelezettségek teljesítéséhez, valamint az esetleges reklamációk kezeléséhez. Ezek között személyes adatok is lehetnek, például a számlázási neved és címed."
                : "Mivel nincs rendelési előzményed, a fiókodat és a mentett személyes adataidat töröljük."}
          </p>
          <div className="mt-auto pt-8">
            <button
              type="button"
              onClick={() => setConfirmationAction("erasure")}
              disabled={saving !== "idle"}
              className="w-fit rounded-sm bg-site-danger px-4 py-2.5 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-60"
            >
              {saving === "erasure" ? "Küldés..." : "Törlést kérek"}
            </button>
          </div>
        </article>
      </section>

      {confirmationAction && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-site-heading/45 px-4"
          role="presentation"
        >
          <section
            className="w-full max-w-md rounded-sm border border-site-border bg-white p-6 shadow-2xl"
            role="alertdialog"
            aria-modal="true"
            aria-labelledby="account-lifecycle-confirmation-title"
          >
            <p className="text-xs font-bold uppercase tracking-[0.14em] text-site-green-dark">
              Fiók kezelése
            </p>
            <h2 id="account-lifecycle-confirmation-title" className="mt-2 text-2xl font-bold text-site-heading">
              {confirmationCopy[confirmationAction].title}
            </h2>
            <div className="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
              <button
                type="button"
                className="rounded-sm border border-site-border bg-white px-4 py-2.5 text-sm font-semibold text-site-heading transition hover:bg-site-surface-soft"
                onClick={() => setConfirmationAction(null)}
              >
                Mégsem
              </button>
              <button
                type="button"
                className={confirmationCopy[confirmationAction].buttonClassName}
                onClick={() =>
                  void submit(confirmationAction === "deactivation" ? 1 : 2, confirmationAction)
                }
              >
                {confirmationCopy[confirmationAction].confirmLabel}
              </button>
            </div>
          </section>
        </div>
      )}
    </div>
  );
}
