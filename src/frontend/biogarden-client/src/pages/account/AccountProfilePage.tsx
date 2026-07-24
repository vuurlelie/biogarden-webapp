import { useEffect, useRef, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import {
  getProfile,
  updateProfile,
} from "../../features/account/api/profileApi";
import type { UserProfile } from "../../features/account/types/profile.types";
import { ROUTES } from "../../shared/constants/routes";
import { getErrorMessage, hasErrorStatus } from "../../shared/utils/errorHandling";

const EMPTY_PROFILE: UserProfile = {
  email: "",
  customerType: 0,
  firstName: null,
  lastName: null,
  phoneNumber: null,
  companyName: null,
  taxNumber: null,
  printedInvoiceRequested: false,
};

function normalizeOptional(value: string): string | null {
  const trimmedValue = value.trim();
  return trimmedValue || null;
}

type ProfileInvalidField = "companyName" | "taxNumber";

export default function AccountProfilePage() {
  const navigate = useNavigate();
  const [profile, setProfile] = useState<UserProfile>(EMPTY_PROFILE);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [invalidField, setInvalidField] = useState<ProfileInvalidField | null>(null);
  const companyNameInputRef = useRef<HTMLInputElement | null>(null);
  const taxNumberInputRef = useRef<HTMLInputElement | null>(null);

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
        if (hasErrorStatus(loadError, 401)) {
          navigate(ROUTES.login, { replace: true });
          return;
        }
        setError(getErrorMessage(loadError, "A profil betöltése nem sikerült."));
      } finally {
        if (!shouldIgnoreResult) setIsLoading(false);
      }
    }

    void loadProfile();
    return () => {
      shouldIgnoreResult = true;
    };
  }, [navigate]);

  function updateField(
    field: "firstName" | "lastName" | "phoneNumber" | "companyName" | "taxNumber",
    value: string,
  ) {
    setProfile((currentProfile) => ({ ...currentProfile, [field]: value }));
    if (field === invalidField) setInvalidField(null);
    setSuccess("");
  }

  function markInvalid(field: ProfileInvalidField, message: string) {
    setInvalidField(field);
    setError(message);
    const inputRefs: Record<ProfileInvalidField, HTMLInputElement | null> = {
      companyName: companyNameInputRef.current,
      taxNumber: taxNumberInputRef.current,
    };
    inputRefs[field]?.focus();
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError("");
    setSuccess("");
    setInvalidField(null);

    if (profile.customerType === 1) {
      if (!profile.companyName?.trim()) {
        markInvalid("companyName", "Céges fiókhoz a cégnév megadása kötelező.");
        return;
      }

      if (!profile.taxNumber?.trim()) {
        markInvalid("taxNumber", "Céges fiókhoz az adószám megadása kötelező.");
        return;
      }

      if (!/^\d{8}(-\d-\d{2})?$/.test(profile.taxNumber.trim())) {
        markInvalid("taxNumber", "Az adószám formátuma hibás. Példa: 12345678-1-12.");
        return;
      }
    }

    try {
      setIsSaving(true);
      await updateProfile({
        customerType: profile.customerType,
        firstName: normalizeOptional(profile.firstName ?? ""),
        lastName: normalizeOptional(profile.lastName ?? ""),
        phoneNumber: normalizeOptional(profile.phoneNumber ?? ""),
        companyName: profile.customerType === 1 ? normalizeOptional(profile.companyName ?? "") : null,
        taxNumber: profile.customerType === 1 ? normalizeOptional(profile.taxNumber ?? "") : null,
        printedInvoiceRequested: profile.customerType === 1 && profile.printedInvoiceRequested,
      });
      setSuccess("A személyes adatok mentése sikerült.");
    } catch (saveError) {
      if (hasErrorStatus(saveError, 401)) {
        navigate(ROUTES.login, { replace: true });
        return;
      }
      setError(getErrorMessage(saveError, "A mentés nem sikerült."));
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
        <div
          className="mt-6 rounded-sm border border-site-green/30 bg-site-surface-soft p-3 text-sm text-site-green-dark"
          role="status"
        >
          {success}
        </div>
      )}

      <form onSubmit={handleSubmit} noValidate className="mt-7 space-y-5">
        <div>
          <label htmlFor="profile-email" className="block text-sm font-semibold text-site-heading">
            Email cím
          </label>
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
            <label htmlFor="profile-last-name" className="block text-sm font-semibold text-site-heading">
              Vezetéknév
            </label>
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
            <label htmlFor="profile-first-name" className="block text-sm font-semibold text-site-heading">
              Keresztnév
            </label>
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
          <label htmlFor="profile-phone" className="block text-sm font-semibold text-site-heading">
            Telefonszám
          </label>
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

        <fieldset className="rounded-sm border border-site-border p-5">
          <legend className="px-2 font-bold text-site-heading">Vásárlói típus</legend>
          <div className="grid gap-3 sm:grid-cols-2">
            <label className="flex cursor-pointer gap-3 rounded-sm border border-site-border p-3 text-sm text-site-text">
              <input
                type="radio"
                name="profileCustomerType"
                checked={profile.customerType === 0}
                onChange={() => {
                  setProfile((currentProfile) => ({
                    ...currentProfile,
                    customerType: 0,
                    companyName: null,
                    taxNumber: null,
                    printedInvoiceRequested: false,
                  }));
                  setInvalidField(null);
                  setSuccess("");
                }}
                className="mt-1 h-4 w-4 accent-site-green"
              />
              <span>
                <strong className="block text-site-heading">Magánszemély</strong>
                A rendeléshez papíralapú nyugtát mellékelünk.
              </span>
            </label>
            <label className="flex cursor-pointer gap-3 rounded-sm border border-site-border p-3 text-sm text-site-text">
              <input
                type="radio"
                name="profileCustomerType"
                checked={profile.customerType === 1}
                onChange={() => {
                  setProfile((currentProfile) => ({ ...currentProfile, customerType: 1 }));
                  setInvalidField(null);
                  setSuccess("");
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

        {profile.customerType === 1 && (
        <fieldset className="rounded-sm border border-site-border p-5">
          <legend className="px-2 font-bold text-site-heading">
            Céges számlázási adatok
          </legend>
          <p className="mb-4 text-xs text-site-muted">
            Céges fiók esetén a cégnév és az adószám megadása kötelező.
          </p>
          <p className="mb-4 text-xs leading-5 text-site-muted">
            Alapértelmezetten elektronikus számlát küldünk emailben. A számlázási címet a Mentett címek oldalon tudod megadni.
          </p>
          <div className="grid gap-5 sm:grid-cols-2">
            <label className="text-sm font-semibold text-site-heading">
              Cégnév
              <input
                ref={companyNameInputRef}
                value={profile.companyName ?? ""}
                onChange={(event) => updateField("companyName", event.target.value)}
                autoComplete="organization"
                className="mt-2 block w-full rounded-sm border border-site-border px-3 py-2.5"
                aria-invalid={invalidField === "companyName"}
              />
            </label>
            <label className="text-sm font-semibold text-site-heading">
              Adószám
              <input
                ref={taxNumberInputRef}
                value={profile.taxNumber ?? ""}
                onChange={(event) => updateField("taxNumber", event.target.value)}
                placeholder="12345678-1-12"
                className="mt-2 block w-full rounded-sm border border-site-border px-3 py-2.5"
                aria-invalid={invalidField === "taxNumber"}
              />
            </label>
          </div>
          <label className="mt-4 flex items-start gap-3 rounded-sm bg-site-surface-soft p-3 text-sm text-site-text">
            <input
              type="checkbox"
              checked={profile.printedInvoiceRequested}
              onChange={(event) => {
                setProfile((currentProfile) => ({
                  ...currentProfile,
                  printedInvoiceRequested: event.target.checked,
                }));
                setSuccess("");
              }}
              className="mt-1 h-4 w-4 accent-site-green"
            />
            <span>Nyomtatott számlát is kérek a rendelés mellé.</span>
          </label>
        </fieldset>
        )}

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
