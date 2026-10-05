import { useCallback, useEffect, useId, useRef, useState } from "react";
import {
  completeErasureRequest,
  confirmAdminUserEmail,
  deactivateAdminUser,
  eraseAdminUser,
  getAdminUsers,
  reactivateAdminUser,
  rejectLifecycleRequest,
  setAdminRole,
} from "../../features/admin/api/adminUsersApi";
import type { AdminUser, AdminUserErasureResult } from "../../features/admin/types/users.types";
import { getAuthSession } from "../../features/auth/utils/authStorage";
import AdminFeedbackMessages from "../../shared/components/admin/AdminFeedbackMessages";
import AdminListEmptyState from "../../shared/components/admin/AdminListEmptyState";
import AdminListSkeletonGrid from "../../shared/components/admin/AdminListSkeletonGrid";
import AdminPageHeader from "../../shared/components/admin/AdminPageHeader";
import AdminPagination from "../../shared/components/admin/AdminPagination";
import { useModalFocus } from "../../shared/hooks/useModalFocus";
import { getErrorMessage, isAbortError } from "../../shared/utils/errorHandling";

const PAGE_SIZE = 12;

type PendingUserAction = {
  title: string;
  question: string;
  confirmLabel: string;
  confirmClassName: string;
  busyLabel: string;
  onConfirm: () => Promise<void>;
};

function AdminUserActionConfirmationModal({
  action,
  busy,
  onCancel,
  onConfirm,
}: {
  action: PendingUserAction;
  busy: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  const dialogRef = useRef<HTMLElement | null>(null);
  const titleId = useId();

  useModalFocus(dialogRef, {
    isOpen: true,
    onClose: () => {
      if (!busy) onCancel();
    },
  });

  return (
    <div className="fixed inset-0 z-[90] grid place-items-center bg-black/55 p-4" role="alertdialog" aria-modal="true" aria-labelledby={titleId}>
      <section ref={dialogRef} tabIndex={-1} className="w-full max-w-md rounded-sm bg-white p-6 shadow-xl outline-none">
        <h2 id={titleId} className="text-xl font-bold text-site-heading">{action.title}</h2>
        <p className="mt-3 leading-6 text-site-text">{action.question}</p>
        <div className="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
          <button
            type="button"
            disabled={busy}
            onClick={onCancel}
            className="rounded-sm border border-site-border px-4 py-3 font-semibold disabled:opacity-60"
          >
            Mégse
          </button>
          <button
            type="button"
            disabled={busy}
            onClick={onConfirm}
            className={action.confirmClassName}
          >
            {busy ? action.busyLabel : action.confirmLabel}
          </button>
        </div>
      </section>
    </div>
  );
}

function isTerminalDeletedUser(user: AdminUser) {
  return user.isAnonymized || user.email.endsWith("@deleted.invalid");
}

function fullName(user: AdminUser) {
  if (isTerminalDeletedUser(user)) return "Törölt felhasználó";

  return [user.lastName, user.firstName].filter(Boolean).join(" ") || "Nincs név";
}

function requestLabel(type: 1 | 2) {
  return type === 1 ? "Deaktiválás" : "Törlés";
}

function UserCard({ user, onUpdate, onErase, onMessage }: {
  user: AdminUser;
  onUpdate: (user: AdminUser) => void;
  onErase: (result: AdminUserErasureResult) => void;
  onMessage: (message: string) => void;
}) {
  const currentUserId = getAuthSession()?.userId;
  const isCurrentUser = currentUserId === user.id;
  const [busy, setBusy] = useState<string | null>(null);
  const [note, setNote] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [pendingAction, setPendingAction] = useState<PendingUserAction | null>(null);
  const noteId = `admin-user-request-note-${user.id}`;
  const terminalDeletedUser = isTerminalDeletedUser(user);
  const hasActiveOrders = user.activeOrderCount > 0;
  const eraseLabel = user.orderCount === 0 ? "Fiók törlése" : "Anonimizálás";
  const dangerousButtonClassName = "rounded-sm bg-site-danger px-4 py-2.5 text-sm font-semibold text-white disabled:opacity-60";
  const outlineDangerButtonClassName = "rounded-sm border border-site-danger px-4 py-2.5 text-sm font-semibold text-site-danger disabled:opacity-60";
  const confirmDangerClassName = "rounded-sm bg-site-danger px-4 py-3 font-semibold text-white disabled:opacity-60";
  const confirmPrimaryClassName = "rounded-sm bg-site-green px-4 py-3 font-semibold text-white disabled:opacity-60";
  const canManageLifecycle = !terminalDeletedUser;

  function showSelfActionWarning() {
    setError("A saját fiókod admin jogosultságát nem veheted el, és a saját fiókodat nem deaktiválhatod, nem törölheted és nem anonimizálhatod.");
  }

  async function run(label: string, action: () => Promise<AdminUser>, message: string) {
    setBusy(label);
    setError(null);
    try {
      const updated = await action();
      onUpdate(updated);
      onMessage(message);
    } catch (actionError) {
      setError(getErrorMessage(actionError, "A művelet nem sikerült."));
    } finally {
      setBusy(null);
    }
  }

  async function runErase() {
    setBusy("erase");
    setError(null);
    try {
      const result = await eraseAdminUser(user.id);
      onErase(result);
      onMessage(result.message);
    } catch (actionError) {
      setError(getErrorMessage(actionError, "A művelet nem sikerült."));
    } finally {
      setBusy(null);
    }
  }

  async function confirmPendingAction() {
    if (!pendingAction) return;

    try {
      await pendingAction.onConfirm();
    } finally {
      setPendingAction(null);
    }
  }

  return (
    <article className="min-w-0 rounded-sm border border-site-border bg-white p-5 shadow-sm">
      <div className="min-w-0">
        <div className="grid min-w-0 grid-cols-1 gap-3 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-start">
          <h2 className="min-w-0 break-words text-lg font-bold text-site-heading">{fullName(user)}</h2>
          <div className="flex flex-wrap gap-2 sm:max-w-[22rem] sm:justify-end">
          {!terminalDeletedUser && isCurrentUser && <span className="rounded-full border border-site-green-dark bg-white px-2.5 py-1 text-xs font-bold text-site-green-dark">Saját fiók</span>}
          {terminalDeletedUser && <span className="rounded-full bg-site-surface-soft px-2.5 py-1 text-xs font-bold text-site-muted">Anonimizált</span>}
          {canManageLifecycle && user.isAdmin && <span className="rounded-full bg-site-green px-2.5 py-1 text-xs font-bold text-white">Admin</span>}
          {canManageLifecycle && (
            <span className={`rounded-full px-2.5 py-1 text-xs font-bold ${user.emailConfirmed ? "bg-site-surface-soft text-site-green-dark" : "bg-site-danger-soft text-site-danger"}`}>
              {user.emailConfirmed ? "Email megerősítve" : "Email nincs megerősítve"}
            </span>
          )}
          {canManageLifecycle && user.isDeactivated && <span className="rounded-full bg-site-danger-soft px-2.5 py-1 text-xs font-bold text-site-danger">Inaktív</span>}
          </div>
        </div>
        {!terminalDeletedUser && <p className="mt-1 break-normal text-sm text-site-muted">{user.email}</p>}
      </div>

      <dl className={`mt-4 grid grid-cols-2 gap-3 text-sm ${terminalDeletedUser ? "" : "sm:grid-cols-3"}`}>
        {!terminalDeletedUser && <div><dt className="text-site-muted">Telefon</dt><dd className="mt-1 font-semibold">{user.phoneNumber ?? "Nincs"}</dd></div>}
        {!terminalDeletedUser && <div><dt className="text-site-muted">Cég</dt><dd className="mt-1 break-words font-semibold">{user.companyName ?? "Nincs"}</dd></div>}
        {!terminalDeletedUser && <div><dt className="text-site-muted">Adószám</dt><dd className="mt-1 break-words font-semibold">{user.taxNumber ?? "Nincs"}</dd></div>}
        <div><dt className="text-site-muted">Rendelések</dt><dd className="mt-1 font-semibold">{user.orderCount}</dd></div>
        <div><dt className="text-site-muted">Aktív rendelés</dt><dd className="mt-1 font-semibold">{user.activeOrderCount}</dd></div>
        {!terminalDeletedUser && <div><dt className="text-site-muted">Szerepkör</dt><dd className="mt-1 break-words font-semibold">{user.roles.join(", ") || "Felhasználó"}</dd></div>}
      </dl>

      {canManageLifecycle && user.pendingRequest && (
        <section className="mt-4 rounded-sm border border-site-green/30 bg-site-surface-soft p-4">
          <h3 className="font-bold text-site-heading">Függő kérelem: {requestLabel(user.pendingRequest.type)}</h3>
          <p className="mt-2 text-sm leading-6 text-site-text">{user.pendingRequest.message}</p>
          {user.pendingRequest.reason && <p className="mt-2 break-words text-sm text-site-muted">Megjegyzés: {user.pendingRequest.reason}</p>}
          <label htmlFor={noteId} className="mt-3 block text-sm font-semibold text-site-heading">
            Admin megjegyzés
          </label>
          <textarea
            id={noteId}
            value={note}
            onChange={(event) => setNote(event.target.value)}
            placeholder="Admin megjegyzés"
            maxLength={1000}
            className="mt-1.5 min-h-20 w-full rounded-sm border border-site-border bg-white px-3 py-2.5 text-sm"
          />
          <div className="mt-3 flex flex-wrap gap-2">
            {user.pendingRequest.type === 2 && (
              <button
                type="button"
                disabled={busy !== null}
                onClick={() => {
                  const pendingRequest = user.pendingRequest;
                  if (!pendingRequest) return;
                  if (isCurrentUser) {
                    showSelfActionWarning();
                    return;
                  }
                  setPendingAction({
                    title: "Törlési kérelem feldolgozása",
                    question: `Biztosan anonimizálod ${user.email} fiókját?`,
                    confirmLabel: "Anonimizálás",
                    confirmClassName: confirmDangerClassName,
                    busyLabel: "Feldolgozás...",
                    onConfirm: () => run("complete", () => completeErasureRequest(pendingRequest.id, note.trim() || null), "A törlési kérelem feldolgozva."),
                  });
                }}
                className={dangerousButtonClassName}
              >
                Anonimizálás
              </button>
            )}
            <button
              type="button"
              disabled={busy !== null}
              onClick={() => {
                const pendingRequest = user.pendingRequest;
                if (!pendingRequest) return;
                setPendingAction({
                  title: "Kérelem elutasítása",
                  question: `Biztosan elutasítod ${user.email} kérelmét?`,
                  confirmLabel: "Elutasítás",
                  confirmClassName: confirmDangerClassName,
                  busyLabel: "Elutasítás...",
                  onConfirm: () => run("reject", () => rejectLifecycleRequest(pendingRequest.id, note.trim() || null), "A kérelmet elutasítottad."),
                });
              }}
              className="rounded-sm border border-site-border px-4 py-2.5 text-sm font-semibold disabled:opacity-60"
            >
              Elutasítás
            </button>
          </div>
        </section>
      )}

      {error && <p role="alert" className="mt-4 rounded-sm bg-site-danger-soft p-3 text-sm text-site-danger">{error}</p>}

      {canManageLifecycle ? (
      <div className="mt-5 flex flex-wrap gap-2 border-t border-site-border pt-4">
        <button
          type="button"
          disabled={busy !== null || user.isAnonymized}
          onClick={() => {
            if (isCurrentUser && user.isAdmin) {
              showSelfActionWarning();
              return;
            }
            setPendingAction({
              title: user.isAdmin ? "Admin jogosultság elvétele" : "Admin jogosultság adása",
              question: user.isAdmin
                ? `Biztosan elveszed ${user.email} admin jogosultságát?`
                : `Biztosan adminná teszed ${user.email} fiókját?`,
              confirmLabel: user.isAdmin ? "Admin elvétele" : "Adminná tétel",
              confirmClassName: user.isAdmin ? confirmDangerClassName : confirmPrimaryClassName,
              busyLabel: "Mentés...",
              onConfirm: () => run("role", () => setAdminRole(user.id, !user.isAdmin), user.isAdmin ? "Admin jogosultság eltávolítva." : "Admin jogosultság hozzáadva."),
            });
          }}
          className="rounded-sm border border-site-green-dark px-4 py-2.5 text-sm font-semibold text-site-green-dark disabled:opacity-60"
        >
          {user.isAdmin ? "Admin elvétele" : "Adminná tétel"}
        </button>
        {!user.emailConfirmed && (
          <button
            type="button"
            disabled={busy !== null || user.isAnonymized}
            onClick={() => {
              setPendingAction({
                title: "Email cím megerősítése",
                question: `Biztosan megerősíted ${user.email} email címét?`,
                confirmLabel: "Megerősítés",
                confirmClassName: confirmPrimaryClassName,
                busyLabel: "Megerősítés...",
                onConfirm: () => run("confirm-email", () => confirmAdminUserEmail(user.id), "Az email cím megerősítve."),
              });
            }}
            className="rounded-sm border border-site-green-dark bg-white px-4 py-2.5 text-sm font-semibold text-site-green-dark disabled:opacity-60"
          >
            Email megerősítése
          </button>
        )}
        {user.isDeactivated ? (
          <button
            type="button"
            disabled={busy !== null || user.isAnonymized}
            onClick={() => {
              setPendingAction({
                title: "Fiók aktiválása",
                question: `Biztosan újraaktiválod ${user.email} fiókját?`,
                confirmLabel: "Aktiválás",
                confirmClassName: confirmPrimaryClassName,
                busyLabel: "Aktiválás...",
                onConfirm: () => run("reactivate", () => reactivateAdminUser(user.id), "A fiók újraaktiválva."),
              });
            }}
            className="rounded-sm bg-site-green px-4 py-2.5 text-sm font-semibold text-white disabled:opacity-60"
          >
            Aktiválás
          </button>
        ) : (
          <button
            type="button"
            disabled={busy !== null}
            onClick={() => {
              if (isCurrentUser) {
                showSelfActionWarning();
                return;
              }
              setPendingAction({
                title: "Fiók deaktiválása",
                question: `Biztosan deaktiválod ${user.email} fiókját?`,
                confirmLabel: "Deaktiválás",
                confirmClassName: confirmDangerClassName,
                busyLabel: "Deaktiválás...",
                onConfirm: () => run("deactivate", () => deactivateAdminUser(user.id), "A fiók deaktiválva."),
              });
            }}
            className={dangerousButtonClassName}
          >
            Deaktiválás
          </button>
        )}
        <button
          type="button"
          disabled={busy !== null || user.isAnonymized || hasActiveOrders}
          title={hasActiveOrders ? "Folyamatban lévő rendelés miatt a fiók most nem törölhető vagy anonimizálható." : undefined}
          onClick={() => {
            if (isCurrentUser) {
              showSelfActionWarning();
              return;
            }
            setPendingAction({
              title: eraseLabel,
              question: user.orderCount === 0
                ? `Biztosan törlöd ${user.email} fiókját?`
                : `Biztosan anonimizálod ${user.email} fiókját?`,
              confirmLabel: eraseLabel,
              confirmClassName: confirmDangerClassName,
              busyLabel: user.orderCount === 0 ? "Törlés..." : "Anonimizálás...",
              onConfirm: runErase,
            });
          }}
          className={outlineDangerButtonClassName}
        >
          {eraseLabel}
        </button>
      </div>
      ) : (
        <p className="mt-5 border-t border-site-border pt-4 text-sm text-site-muted">
          A fiók személyes adatai már anonimizálva lettek, ezért további fiókművelet nem érhető el.
        </p>
      )}
      {hasActiveOrders && !user.isAnonymized && (
        <p className="mt-3 text-sm text-site-muted">
          Folyamatban lévő rendelés miatt a fiók most nem törölhető vagy anonimizálható.
        </p>
      )}
      {pendingAction && (
        <AdminUserActionConfirmationModal
          action={pendingAction}
          busy={busy !== null}
          onCancel={() => setPendingAction(null)}
          onConfirm={() => void confirmPendingAction()}
        />
      )}
    </article>
  );
}

export default function AdminAccountsPage() {
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [role, setRole] = useState<"all" | "admin" | "customer">("all");
  const [status, setStatus] = useState<"all" | "active" | "inactive" | "request">("all");
  const [page, setPage] = useState(1);

  const [pages, setTotalPages] = useState(1);
  const requestId = useRef(0);
  const mounted = useRef(true);

  const load = useCallback(async (signal?: AbortSignal, background = false) => {
    if (!mounted.current) return;
    const id = ++requestId.current;
    if (!background) setLoading(true);
    setError(null);
    try {
      const result = await getAdminUsers({ page, pageSize: PAGE_SIZE, search, role, status }, signal);
      if (!mounted.current || signal?.aborted || id !== requestId.current) return;
      setUsers(result.items);
      setTotalPages(result.totalPages);
      if (result.page !== page) setPage(result.page);
    } catch (loadError) {
      if (mounted.current && !signal?.aborted && id === requestId.current && !isAbortError(loadError)) {
        setError(getErrorMessage(loadError, "A felhasználók betöltése nem sikerült."));
      }
    } finally {
      if (mounted.current && !signal?.aborted && id === requestId.current) setLoading(false);
    }
  }, [page, search, role, status]);

  const latestLoad = useRef(load);
  useEffect(() => {
    latestLoad.current = load;
    mounted.current = true;
    ++requestId.current;
    const controller = new AbortController();
    const timer = window.setTimeout(() => void load(controller.signal), search.trim() ? 250 : 0);
    return () => { mounted.current = false; window.clearTimeout(timer); controller.abort(); };
  }, [load, search]);

  useEffect(() => {
    if (!message) return;
    const timer = window.setTimeout(() => setMessage(null), 3500);
    return () => clearTimeout(timer);
  }, [message]);

  const current = page;
  const shown = users;

  function updateUser(updated: AdminUser) {
    setUsers((items) => items.map((item) => item.id === updated.id ? updated : item));
    void latestLoad.current(undefined, true);
  }

  function eraseUser(result: AdminUserErasureResult) {
    setUsers((items) => result.user
      ? items.map((item) => item.id === result.userId ? result.user! : item).filter((item) => !isTerminalDeletedUser(item))
      : items.filter((item) => item.id !== result.userId));
    void latestLoad.current(undefined, true);
  }

  return (
    <div className="space-y-6">
      <AdminPageHeader title="Felhasználók" description="Regisztrált felhasználók, jogosultságok és adatkezelési kérelmek." />

      <AdminFeedbackMessages message={message} error={error} />

      <section className="grid gap-4 rounded-sm border border-site-border bg-white p-4 shadow-sm md:grid-cols-3">
        <label htmlFor="admin-account-search" className="text-sm font-semibold">
          Keresés
          <input id="admin-account-search" maxLength={200}
            value={search} onChange={(event) => { setSearch(event.target.value); setPage(1); }} placeholder="Név, email, telefon, cég" className="mt-1.5 w-full rounded-sm border border-site-border px-3 py-2.5" />
        </label>
        <label htmlFor="admin-account-role" className="text-sm font-semibold">
          Szerepkör
          <select id="admin-account-role" value={role} onChange={(event) => { setRole(event.target.value as typeof role); setPage(1); }} className="mt-1.5 w-full rounded-sm border border-site-border px-3 py-2.5">
            <option value="all">Minden szerepkör</option>
            <option value="admin">Admin</option>
            <option value="customer">Felhasználó</option>
          </select>
        </label>
        <label htmlFor="admin-account-status" className="text-sm font-semibold">
          Állapot
          <select id="admin-account-status" value={status} onChange={(event) => { setStatus(event.target.value as typeof status); setPage(1); }} className="mt-1.5 w-full rounded-sm border border-site-border px-3 py-2.5">
            <option value="all">Minden állapot</option>
            <option value="active">Aktív</option>
            <option value="inactive">Inaktív</option>
            <option value="request">Függő kérelem</option>
          </select>
        </label>
      </section>

      {loading ? (
        <AdminListSkeletonGrid gridClassName="grid gap-5 xl:grid-cols-2" itemHeightClassName="h-72" />
      ) : shown.length === 0 ? (
        <AdminListEmptyState />
      ) : (
        <section className="grid min-w-0 items-start gap-5 xl:grid-cols-2">
          {shown.map((user) => <UserCard key={user.id} user={user} onUpdate={updateUser} onErase={eraseUser} onMessage={setMessage} />)}
        </section>
      )}

      {!loading && users.length > 0 && <AdminPagination page={current} totalPages={pages} onPageChange={setPage} />}
    </div>
  );
}
