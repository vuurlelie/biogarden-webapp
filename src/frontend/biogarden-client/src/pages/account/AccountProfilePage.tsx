import { useEffect, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import {
  getProfile,
  ProfileApiError,
  updateProfile,
} from "../../features/account/api/profileApi";
import type { UserProfile } from "../../features/account/types/profile.types";
import { ROUTES } from "../../shared/constants/routes";

const EMPTY_PROFILE: UserProfile = {
  email: "",
  firstName: null,
  lastName: null,
  phoneNumber: null,
};

function normalizeOptional(value: string): string | null {
  const trimmedValue = value.trim();
  return trimmedValue || null;
}

export default function AccountProfilePage() {
  const navigate = useNavigate();
  const [profile, setProfile] = useState<UserProfile>(EMPTY_PROFILE);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  useEffect(() => {
    let shouldIgnoreResult = false;

    async function loadProfile() {
      try {
        setIsLoading(true);
        setError("");
        const loadedProfile = await getProfile();
        if (!shouldIgnoreResult) setProfile(loadedProfile);
      } catch (loadError) {
        if (shouldIgnoreResult) return;
        if (loadError instanceof ProfileApiError && loadError.status === 401) {
          navigate(ROUTES.login, { replace: true });
          return;
        }
        setError(loadError instanceof Error ? loadError.message : "A profil betöltése nem sikerült.");
      } finally {
        if (!shouldIgnoreResult) setIsLoading(false);
      }
    }

    void loadProfile();
    return () => {
      shouldIgnoreResult = true;
    };
  }, [navigate]);

  function updateField(field: "firstName" | "lastName" | "phoneNumber", value: string) {
    setProfile((currentProfile) => ({ ...currentProfile, [field]: value }));
    setSuccess("");
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError("");
    setSuccess("");

    try {
      setIsSaving(true);
      await updateProfile({
        firstName: normalizeOptional(profile.firstName ?? ""),
        lastName: normalizeOptional(profile.lastName ?? ""),
        phoneNumber: normalizeOptional(profile.phoneNumber ?? ""),
      });
      setSuccess("A személyes adatok mentése sikerült.");
    } catch (saveError) {
      if (saveError instanceof ProfileApiError && saveError.status === 401) {
        navigate(ROUTES.login, { replace: true });
        return;
      }
      setError(saveError instanceof Error ? saveError.message : "A mentés nem sikerült.");
    } finally {
      setIsSaving(false);
    }
  }

  if (isLoading) {
    return (
      <div className="rounded-sm bg-site-surface-soft p-10 text-center shadow-md">
        Profil betöltése...
      </div>
    );
  }

  return (
    <div className="rounded-sm border border-site-border bg-white p-6 shadow-lg sm:p-8">
      <p className="text-xs font-bold uppercase tracking-[0.15em] text-site-green-dark">Fiókadatok</p>
      <h1 className="mt-2 text-3xl font-bold text-site-heading">Személyes adatok</h1>
      <p className="mt-2 text-sm leading-6 text-site-text">
        Ezeket az adatokat a rendelések és a kapcsolattartás során használjuk.
      </p>

      {error && (
        <div className="mt-6 rounded-sm bg-site-danger-soft p-3 text-sm text-site-danger" role="alert">
          {error}
        </div>
      )}
      {success && (
        <div className="mt-6 rounded-sm border border-site-green/30 bg-site-surface-soft p-3 text-sm text-site-green-dark" role="status">
          {success}
        </div>
      )}

      <form onSubmit={handleSubmit} className="mt-7 space-y-5">
        <div>
          <label htmlFor="profile-email" className="block text-sm font-semibold text-site-heading">Email cím</label>
          <input
            id="profile-email"
            type="email"
            value={profile.email}
            disabled
            className="mt-2 block w-full rounded-sm border border-site-border bg-site-surface-soft px-3 py-2.5 text-site-muted"
          />
          <p className="mt-1 text-xs text-site-muted">Az email cím jelenleg nem módosítható.</p>
        </div>

        <div className="grid gap-5 sm:grid-cols-2">
          <div>
            <label htmlFor="profile-last-name" className="block text-sm font-semibold text-site-heading">Vezetéknév</label>
            <input
              id="profile-last-name"
              type="text"
              autoComplete="family-name"
              value={profile.lastName ?? ""}
              onChange={(event) => updateField("lastName", event.target.value)}
              className="mt-2 block w-full rounded-sm border border-site-border px-3 py-2.5 text-site-heading outline-none focus:border-site-green focus:ring-2 focus:ring-site-green/20"
            />
          </div>
          <div>
            <label htmlFor="profile-first-name" className="block text-sm font-semibold text-site-heading">Keresztnév</label>
            <input
              id="profile-first-name"
              type="text"
              autoComplete="given-name"
              value={profile.firstName ?? ""}
              onChange={(event) => updateField("firstName", event.target.value)}
              className="mt-2 block w-full rounded-sm border border-site-border px-3 py-2.5 text-site-heading outline-none focus:border-site-green focus:ring-2 focus:ring-site-green/20"
            />
          </div>
        </div>

        <div>
          <label htmlFor="profile-phone" className="block text-sm font-semibold text-site-heading">Telefonszám</label>
          <input
            id="profile-phone"
            type="tel"
            autoComplete="tel"
            value={profile.phoneNumber ?? ""}
            onChange={(event) => updateField("phoneNumber", event.target.value)}
            className="mt-2 block w-full rounded-sm border border-site-border px-3 py-2.5 text-site-heading outline-none focus:border-site-green focus:ring-2 focus:ring-site-green/20"
            placeholder="+36 20 123 4567"
          />
        </div>

        <div className="pt-2">
          <button
            type="submit"
            disabled={isSaving}
            className="rounded-sm bg-site-green px-6 py-3 font-semibold text-white transition hover:bg-site-green-hover disabled:cursor-not-allowed disabled:opacity-60"
          >
            {isSaving ? "Mentés..." : "Adatok mentése"}
          </button>
        </div>
      </form>
    </div>
  );
}
