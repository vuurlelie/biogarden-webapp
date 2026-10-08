import { getServerFieldErrors } from "../../shared/utils/errorHandling";
import { useEffect, useRef, useState, type FormEvent } from "react";
import {
  getProfile,
  updateProfile,
} from "../../features/account/api/profileApi";
import AccountFeedback from "../../features/account/components/AccountFeedback";
import AccountLoadingPanel from "../../features/account/components/AccountLoadingPanel";
import AccountPageShell from "../../features/account/components/AccountPageShell";
import { useAccountErrorHandler } from "../../features/account/hooks/useAccountErrorHandler";
import type { UserProfile } from "../../features/account/types/profile.types";

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
type ProfileFieldErrors = Partial<Record<ProfileInvalidField, string>>;

export default function AccountProfilePage() {
  const handleAccountError = useAccountErrorHandler();
  const [profile, setProfile] = useState<UserProfile>(EMPTY_PROFILE);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [pageError, setPageError] = useState("");
  const [formError, setFormError] = useState("");
  const [success, setSuccess] = useState("");
  const [fieldErrors, setFieldErrors] = useState<ProfileFieldErrors>({});
  const [showCompanyDataChoice, setShowCompanyDataChoice] = useState(false);
  const companyNameInputRef = useRef<HTMLInputElement | null>(null);
  const taxNumberInputRef = useRef<HTMLInputElement | null>(null);

  const inputClassName =
    "mt-2 block w-full rounded-sm border border-site-border px-3 py-2.5 text-site-heading outline-none focus:border-site-green focus:ring-2 focus:ring-site-green/20";
  const invalidInputClassName =
    "mt-2 block w-full rounded-sm border border-site-danger bg-site-danger-soft px-3 py-2.5 text-site-heading outline-none focus:border-site-danger focus:ring-2 focus:ring-site-danger/20";

  function getInputClassName(field: ProfileInvalidField) {
    return fieldErrors[field] ? invalidInputClassName : inputClassName;
  }

  function getErrorId(field: ProfileInvalidField) {
    return `profile-${field}-error`;
  }

  function renderFieldError(field: ProfileInvalidField) {
    if (!fieldErrors[field]) return null;

    return (
      <p id={getErrorId(field)} className="mt-1 text-xs font-normal text-site-danger">
        {fieldErrors[field]}
      </p>
    );
  }

  function clearFieldError(field: ProfileInvalidField) {
    setFieldErrors((current) => {
      if (!current[field]) return current;
      const next = { ...current };
      delete next[field];
      return next;
    });
  }

  useEffect(() => {
    let shouldIgnoreResult = false;

    async function loadProfile() {
      try {
        setIsLoading(true);
        setPageError("");
        const loadedProfile = await getProfile();
        if (!shouldIgnoreResult) setProfile(loadedProfile);
      } catch (loadError) {
        if (shouldIgnoreResult) return;
        setPageError(handleAccountError(loadError, "A profil betöltése nem sikerült.") ?? "");
      } finally {
        if (!shouldIgnoreResult) setIsLoading(false);
      }
    }

    void loadProfile();
    return () => {
      shouldIgnoreResult = true;
    };
  }, [handleAccountError]);

  function updateField(
    field: "firstName" | "lastName" | "phoneNumber" | "companyName" | "taxNumber",
    value: string,
  ) {
    setProfile((currentProfile) => ({ ...currentProfile, [field]: value }));
    if (field === "companyName" || field === "taxNumber") clearFieldError(field);
    setFormError("");
    setSuccess("");
  }

  function markInvalid(field: ProfileInvalidField, message: string) {
    setFormError("");
    setFieldErrors({ [field]: message });
    const inputRefs: Record<ProfileInvalidField, HTMLInputElement | null> = {
      companyName: companyNameInputRef.current,
      taxNumber: taxNumberInputRef.current,
    };
    inputRefs[field]?.focus();
  }

  function hasSavedCompanyData() {
    return Boolean(profile.companyName?.trim() || profile.taxNumber?.trim() || profile.printedInvoiceRequested);
  }

  function switchToPrivate(keepCompanyData: boolean) {
    setProfile((currentProfile) => ({
      ...currentProfile,
      customerType: 0,
      companyName: keepCompanyData ? currentProfile.companyName : null,
      taxNumber: keepCompanyData ? currentProfile.taxNumber : null,
      printedInvoiceRequested: false,
    }));
    setShowCompanyDataChoice(false);
    setFieldErrors({});
    setFormError("");
    setSuccess("");
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError("");
    setSuccess("");
    setFieldErrors({});

    if (profile.taxNumber?.trim()) {
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
        companyName: normalizeOptional(profile.companyName ?? ""),
        taxNumber: normalizeOptional(profile.taxNumber ?? ""),
        printedInvoiceRequested: profile.customerType === 1 && profile.printedInvoiceRequested,
      });
      setSuccess("A személyes adatok mentése sikerült.");
    } catch (saveError) {
      setFieldErrors(getServerFieldErrors(saveError, ["companyName", "taxNumber"] as const));
      setFormError(handleAccountError(saveError, "A mentés nem sikerült.") ?? "");
    } finally {
      setIsSaving(false);
    }
  }

  if (isLoading) {
    return <AccountLoadingPanel label="Profil betöltése..." />;
  }

  return (
    <AccountPageShell
      title="Személyes adatok"
      description="Ezeket az adatokat a rendelések és a kapcsolattartás során használjuk."
    >
      <AccountFeedback error={pageError} success="" />

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
          <p className="mt-1 text-xs text-site-muted">Az email cím nem módosítható.</p>
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
          <legend className="px-2 font-bold text-site-heading">Alapértelmezett rendelési mód</legend>
          <div className="grid gap-3 sm:grid-cols-2">
            <label className="flex cursor-pointer gap-3 rounded-sm border border-site-border p-3 text-sm text-site-text">
              <input
                type="radio"
                name="profileCustomerType"
                checked={profile.customerType === 0}
                onChange={() => {
                  if (profile.customerType === 1 && hasSavedCompanyData()) {
                    setShowCompanyDataChoice(true);
                    return;
                  }

                  switchToPrivate(false);
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
                  setShowCompanyDataChoice(false);
                  setFieldErrors({});
                  setFormError("");
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

        {showCompanyDataChoice && (
          <div
            className="rounded-sm border border-site-border bg-site-surface-soft p-4 shadow-sm"
            role="alertdialog"
            aria-labelledby="company-data-choice-title"
            aria-describedby="company-data-choice-description"
          >
            <h2 id="company-data-choice-title" className="text-base font-bold text-site-heading">
              Megtartsuk a céges adataidat?
            </h2>
            <p id="company-data-choice-description" className="mt-2 text-sm leading-6 text-site-text">
              Magánszemélyes rendelési módban nem használjuk a céges adatokat. Megtarthatod őket későbbre, ha később újra céges rendelési módra váltanál.
            </p>
            <div className="mt-4 flex flex-col gap-2 sm:flex-row">
              <button
                type="button"
                className="rounded-sm bg-site-green px-4 py-2 text-sm font-semibold text-white transition hover:bg-site-green-hover"
                onClick={() => switchToPrivate(true)}
              >
                Megtartom későbbre
              </button>
              <button
                type="button"
                className="rounded-sm border border-site-border bg-white px-4 py-2 text-sm font-semibold text-site-heading transition hover:bg-site-surface"
                onClick={() => switchToPrivate(false)}
              >
                Törlöm a céges adatokat
              </button>
              <button
                type="button"
                className="rounded-sm px-4 py-2 text-sm font-semibold text-site-green-dark transition hover:bg-white"
                onClick={() => setShowCompanyDataChoice(false)}
              >
                Mégsem
              </button>
            </div>
          </div>
        )}

        {profile.customerType === 1 && (
        <fieldset className="rounded-sm border border-site-border p-5">
          <legend className="px-2 font-bold text-site-heading">
            Céges számlázási adatok
          </legend>
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
                className={getInputClassName("companyName")}
                aria-invalid={Boolean(fieldErrors.companyName)}
                aria-describedby={fieldErrors.companyName ? getErrorId("companyName") : undefined}
              />
              {renderFieldError("companyName")}
            </label>
            <label className="text-sm font-semibold text-site-heading">
              Adószám
              <input
                ref={taxNumberInputRef}
                value={profile.taxNumber ?? ""}
                onChange={(event) => updateField("taxNumber", event.target.value)}
                placeholder="12345678-1-12"
                className={getInputClassName("taxNumber")}
                aria-invalid={Boolean(fieldErrors.taxNumber)}
                aria-describedby={fieldErrors.taxNumber ? getErrorId("taxNumber") : undefined}
              />
              {renderFieldError("taxNumber")}
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

        <div className="flex flex-col gap-3 pt-2 sm:flex-row sm:items-center">
          <button
            type="submit"
            disabled={isSaving}
            className="w-full rounded-sm bg-site-green px-6 py-3 font-semibold text-white transition hover:bg-site-green-hover disabled:cursor-not-allowed disabled:opacity-60 sm:w-auto"
          >
            {isSaving ? "Mentés..." : "Adatok mentése"}
          </button>

          {success && (
            <p
              className="rounded-sm border border-site-green/30 bg-site-surface-soft px-3 py-2 text-sm text-site-green-dark"
              role="status"
            >
              {success}
            </p>
          )}

          {formError && (
            <p className="rounded-sm bg-site-danger-soft px-3 py-2 text-sm text-site-danger" role="alert">
              {formError}
            </p>
          )}
        </div>
      </form>
    </AccountPageShell>
  );
}
