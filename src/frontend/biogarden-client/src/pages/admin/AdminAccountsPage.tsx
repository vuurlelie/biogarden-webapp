import { useCallback, useEffect, useMemo, useState } from "react";
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
import AdminPageHeader from "../../shared/components/admin/AdminPageHeader";
import AdminPagination from "../../shared/components/admin/AdminPagination";

const PAGE_SIZE = 12;

function fullName(user: AdminUser) {
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
  const noteId = `admin-user-request-note-${user.id}`;

  function showSelfActionWarning() {
    window.alert("A saját fiókod admin jogosultságát nem veheted el, és a saját fiókodat nem deaktiválhatod, nem törölheted és nem anonimizálhatod.");
  }

  async function run(label: string, action: () => Promise<AdminUser>, message: string) {
    setBusy(label);
    setError(null);
    try {
      const updated = await action();
      onUpdate(updated);
      onMessage(message);
    } catch (actionError) {
      setError(actionError instanceof Error ? actionError.message : "A művelet nem sikerült.");
    } finally {
      setBusy(null);
    }
  }

  async function runErase() {
    if (isCurrentUser) {
      showSelfActionWarning();
      return;
    }

    const actionLabel = user.orderCount === 0 ? "törlöd" : "anonimizálod";
    if (!window.confirm(`Biztosan ${actionLabel} ezt a fiókot?`)) return;

    setBusy("erase");
    setError(null);
    try {
      const result = await eraseAdminUser(user.id);
      onErase(result);
      onMessage(result.message);
    } catch (actionError) {
      setError(actionError instanceof Error ? actionError.message : "A művelet nem sikerült.");
    } finally {
      setBusy(null);
    }
  }

  return (
    <article className="min-w-0 rounded-sm border border-site-border bg-white p-5 shadow-sm">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div className="min-w-0">
          <h2 className="break-words text-lg font-bold text-site-heading">{fullName(user)}</h2>
          <p className="mt-1 break-words text-sm text-site-muted">{user.email}</p>
        </div>
        <div className="flex flex-wrap gap-2">
          {isCurrentUser && <span className="rounded-full border border-site-green-dark bg-white px-2.5 py-1 text-xs font-bold text-site-green-dark">Saját fiók</span>}
          {user.isAdmin && <span className="rounded-full bg-site-green px-2.5 py-1 text-xs font-bold text-white">Admin</span>}
          <span className={`rounded-full px-2.5 py-1 text-xs font-bold ${user.emailConfirmed ? "bg-site-surface-soft text-site-green-dark" : "bg-site-danger-soft text-site-danger"}`}>
            {user.emailConfirmed ? "Email megerősítve" : "Email nincs megerősítve"}
          </span>
          {user.isDeactivated && <span className="rounded-full bg-site-danger-soft px-2.5 py-1 text-xs font-bold text-site-danger">Inaktív</span>}
          {user.isAnonymized && <span className="rounded-full bg-site-surface-soft px-2.5 py-1 text-xs font-bold text-site-muted">Anonimizált</span>}
        </div>
      </div>

      <dl className="mt-4 grid grid-cols-2 gap-3 text-sm sm:grid-cols-3">
        <div><dt className="text-site-muted">Telefon</dt><dd className="mt-1 font-semibold">{user.phoneNumber ?? "Nincs"}</dd></div>
        <div><dt className="text-site-muted">Cég</dt><dd className="mt-1 break-words font-semibold">{user.companyName ?? "Nincs"}</dd></div>
        <div><dt className="text-site-muted">Adószám</dt><dd className="mt-1 break-words font-semibold">{user.taxNumber ?? "Nincs"}</dd></div>
        <div><dt className="text-site-muted">Rendelések</dt><dd className="mt-1 font-semibold">{user.orderCount}</dd></div>
        <div><dt className="text-site-muted">Aktív rendelés</dt><dd className="mt-1 font-semibold">{user.activeOrderCount}</dd></div>
        <div><dt className="text-site-muted">Szerepkör</dt><dd className="mt-1 break-words font-semibold">{user.roles.join(", ") || "Felhasználó"}</dd></div>
      </dl>

      {user.pendingRequest && (
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
                  if (isCurrentUser) {
                    showSelfActionWarning();
                    return;
                  }
                  void run("complete", () => completeErasureRequest(user.pendingRequest!.id, note.trim() || null), "A törlési kérelem feldolgozva.");
                }}
                className="rounded-sm bg-site-green px-4 py-2.5 text-sm font-semibold text-white disabled:opacity-60"
              >
                Anonimizálás
              </button>
            )}
            <button
              type="button"
              disabled={busy !== null}
              onClick={() => void run("reject", () => rejectLifecycleRequest(user.pendingRequest!.id, note.trim() || null), "A kérelmet elutasítottad.")}
              className="rounded-sm border border-site-border px-4 py-2.5 text-sm font-semibold disabled:opacity-60"
            >
              Elutasítás
            </button>
          </div>
        </section>
      )}

      {error && <p role="alert" className="mt-4 rounded-sm bg-site-danger-soft p-3 text-sm text-site-danger">{error}</p>}

      <div className="mt-5 flex flex-wrap gap-2 border-t border-site-border pt-4">
        <button
          type="button"
          disabled={busy !== null || user.isAnonymized}
          onClick={() => {
            if (isCurrentUser && user.isAdmin) {
              showSelfActionWarning();
              return;
            }
            void run("role", () => setAdminRole(user.id, !user.isAdmin), user.isAdmin ? "Admin jogosultság eltávolítva." : "Admin jogosultság hozzáadva.");
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
              if (!window.confirm("Biztosan megerősíted a felhasználó email címét? Csak akkor használd, ha a felhasználó a saját email címéről kérte.")) return;
              void run("confirm-email", () => confirmAdminUserEmail(user.id), "Az email cím megerősítve.");
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
            onClick={() => void run("reactivate", () => reactivateAdminUser(user.id), "A fiók újraaktiválva.")}
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
              void run("deactivate", () => deactivateAdminUser(user.id), "A fiók deaktiválva.");
            }}
            className="rounded-sm bg-site-danger px-4 py-2.5 text-sm font-semibold text-white disabled:opacity-60"
          >
            Deaktiválás
          </button>
        )}
        <button
          type="button"
          disabled={busy !== null || user.isAnonymized || user.activeOrderCount > 0}
          title={user.activeOrderCount > 0 ? "Folyamatban lévő rendelés miatt a fiók most nem törölhető." : undefined}
          onClick={() => void runErase()}
          className="rounded-sm border border-site-danger px-4 py-2.5 text-sm font-semibold text-site-danger disabled:opacity-60"
        >
          {user.orderCount === 0 ? "Fiók törlése" : "Anonimizálás"}
        </button>
      </div>
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

  const load = useCallback(async (signal?: AbortSignal) => {
    setLoading(true);
    setError(null);
    try {
      setUsers(await getAdminUsers(signal));
    } catch (loadError) {
      if (!(loadError instanceof DOMException && loadError.name === "AbortError")) {
        setError(loadError instanceof Error ? loadError.message : "A felhasználók betöltése nem sikerült.");
      }
    } finally {
      if (!signal?.aborted) setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    if (!message) return;
    const timer = window.setTimeout(() => setMessage(null), 3500);
    return () => clearTimeout(timer);
  }, [message]);

  const filtered = useMemo(() => {
    const query = search.trim().toLocaleLowerCase("hu-HU");
    return users.filter((user) => {
      if (query && ![fullName(user), user.email, user.phoneNumber ?? "", user.companyName ?? "", user.taxNumber ?? ""].some((value) => value.toLocaleLowerCase("hu-HU").includes(query))) return false;
      if (role === "admin" && !user.isAdmin) return false;
      if (role === "customer" && user.isAdmin) return false;
      if (status === "active" && user.isDeactivated) return false;
      if (status === "inactive" && !user.isDeactivated) return false;
      if (status === "request" && !user.pendingRequest) return false;
      return true;
    });
  }, [users, search, role, status]);

  const pages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const current = Math.min(page, pages);
  const shown = filtered.slice((current - 1) * PAGE_SIZE, current * PAGE_SIZE);

  function updateUser(updated: AdminUser) {
    setUsers((items) => items.map((item) => item.id === updated.id ? updated : item));
  }

  function eraseUser(result: AdminUserErasureResult) {
    setUsers((items) => result.user
      ? items.map((item) => item.id === result.userId ? result.user! : item)
      : items.filter((item) => item.id !== result.userId));
  }

  return (
    <div className="space-y-6">
      <AdminPageHeader title="Felhasználók" description="Regisztrált felhasználók, jogosultságok és adatkezelési kérelmek." />

      {message && <div role="status" className="rounded-sm bg-site-surface-soft p-4 font-semibold text-site-green-dark">{message}</div>}
      {error && <div role="alert" className="rounded-sm bg-site-danger-soft p-4 text-site-danger">{error}</div>}

      <section className="grid gap-4 rounded-sm border border-site-border bg-white p-4 shadow-sm md:grid-cols-3">
        <label htmlFor="admin-account-search" className="text-sm font-semibold">
          Keresés
          <input id="admin-account-search" value={search} onChange={(event) => { setSearch(event.target.value); setPage(1); }} placeholder="Név, email, telefon, cég" className="mt-1.5 w-full rounded-sm border border-site-border px-3 py-2.5" />
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
        <div className="grid gap-5 xl:grid-cols-2">
          {Array.from({ length: 6 }, (_, index) => <div key={index} className="h-72 animate-pulse rounded-sm bg-white" />)}
        </div>
      ) : shown.length === 0 ? (
        <div className="rounded-sm border border-site-border bg-white p-8 text-center">Nincs találat.</div>
      ) : (
        <section className="grid min-w-0 items-start gap-5 xl:grid-cols-2">
          {shown.map((user) => <UserCard key={user.id} user={user} onUpdate={updateUser} onErase={eraseUser} onMessage={setMessage} />)}
        </section>
      )}

      {!loading && filtered.length > 0 && <AdminPagination page={current} totalPages={pages} onPageChange={setPage} />}
    </div>
  );
}
