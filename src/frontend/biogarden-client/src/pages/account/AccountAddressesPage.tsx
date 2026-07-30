import { useEffect, useState, type FormEvent } from "react";
import {
  getAddresses,
  getProfile,
  updateAddresses,
} from "../../features/account/api/profileApi";
import AccountFeedback from "../../features/account/components/AccountFeedback";
import AccountLoadingPanel from "../../features/account/components/AccountLoadingPanel";
import AccountPageShell from "../../features/account/components/AccountPageShell";
import { useAccountErrorHandler } from "../../features/account/hooks/useAccountErrorHandler";
import type {
  AddressDetails,
  AddressFormData,
} from "../../features/account/types/address.types";
import AddressFields from "../../shared/components/forms/AddressFields";
import { isPostalCodeCityPairValid } from "../../shared/utils/postalCodeLookup";

const EMPTY_ADDRESS: AddressFormData = {
  postalCode: "",
  city: "",
  street: "",
  houseNumber: "",
  building: null,
  floor: null,
  door: null,
  doorbell: null,
};

type AddressFieldErrors = Partial<Record<keyof AddressFormData, string>>;

function toFormData(address: AddressDetails | null): AddressFormData {
  if (!address) return { ...EMPTY_ADDRESS };
  return {
    postalCode: address.postalCode,
    city: address.city,
    street: address.street,
    houseNumber: address.houseNumber,
    building: address.building,
    floor: address.floor,
    door: address.door,
    doorbell: address.doorbell,
  };
}

function normalizeAddress(address: AddressFormData): AddressFormData {
  const normalizeOptional = (value: string | null) => value?.trim() || null;
  return {
    postalCode: address.postalCode.trim(),
    city: address.city.trim(),
    street: address.street.trim(),
    houseNumber: address.houseNumber.trim(),
    building: normalizeOptional(address.building),
    floor: normalizeOptional(address.floor),
    door: normalizeOptional(address.door),
    doorbell: normalizeOptional(address.doorbell),
  };
}

function hasAddressValue(address: AddressFormData) {
  return Object.values(address).some((value) => typeof value === "string" && value.trim().length > 0);
}

function getAddressSaveSuccessMessage(
  customerType: 0 | 1,
  useShippingAsBilling: boolean,
  hasShippingAddress: boolean,
  hasBillingAddress: boolean,
) {
  if (!hasShippingAddress && !hasBillingAddress) {
    return "Nincs mentett cím a fiókodban.";
  }

  if (customerType === 0) {
    return "A szállítási cím mentése sikerült.";
  }

  if (hasShippingAddress && !hasBillingAddress && !useShippingAsBilling) {
    return "A szállítási cím mentése sikerült.";
  }

  if (!hasShippingAddress && hasBillingAddress) {
    return "A számlázási cím mentése sikerült.";
  }

  if (useShippingAsBilling) {
    return "A szállítási cím mentése sikerült, ezt használjuk számlázási címként is.";
  }

  return "A szállítási és számlázási cím mentése sikerült.";
}

export default function AccountAddressesPage() {
  const handleAccountError = useAccountErrorHandler();
  const [customerType, setCustomerType] = useState<0 | 1>(0);
  const [shippingAddress, setShippingAddress] = useState<AddressFormData>({ ...EMPTY_ADDRESS });
  const [billingAddress, setBillingAddress] = useState<AddressFormData>({ ...EMPTY_ADDRESS });
  const [useShippingAsBilling, setUseShippingAsBilling] = useState(true);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [pageError, setPageError] = useState("");
  const [formError, setFormError] = useState("");
  const [success, setSuccess] = useState("");
  const [shippingErrors, setShippingErrors] = useState<AddressFieldErrors>({});
  const [billingErrors, setBillingErrors] = useState<AddressFieldErrors>({});

  useEffect(() => {
    let shouldIgnoreResult = false;

    async function loadAddresses() {
      try {
        const [addresses, profile] = await Promise.all([getAddresses(), getProfile()]);
        if (shouldIgnoreResult) return;

        setCustomerType(profile.customerType);
        setShippingAddress(toFormData(addresses.shippingAddress));
        setBillingAddress(toFormData(addresses.billingAddress));
        setUseShippingAsBilling(
          addresses.billingAddress === null ||
            addresses.shippingAddress?.id === addresses.billingAddress.id,
        );
      } catch (loadError) {
        if (shouldIgnoreResult) return;
        setPageError(handleAccountError(loadError, "A címek betöltése nem sikerült.") ?? "");
      } finally {
        if (!shouldIgnoreResult) setIsLoading(false);
      }
    }

    void loadAddresses();
    return () => {
      shouldIgnoreResult = true;
    };
  }, [handleAccountError]);

  function validateAddress(address: AddressFormData, label: "shipping" | "billing") {
    const errors: AddressFieldErrors = {};
    const prefix = label === "shipping" ? "Szállítási" : "Számlázási";

    if (!hasAddressValue(address)) {
      return errors;
    }

    if (!address.postalCode.trim()) errors.postalCode = `${prefix} irányítószám megadása kötelező.`;
    else if (!/^\d{4}$/.test(address.postalCode.trim())) {
      errors.postalCode = `${prefix} irányítószámként 4 számjegyet adj meg.`;
    }
    if (!address.city.trim()) errors.city = `${prefix} település megadása kötelező.`;
    else if (!errors.postalCode && !isPostalCodeCityPairValid(address.postalCode, address.city)) {
      errors.city = `${prefix} település nem tartozik ehhez az irányítószámhoz.`;
    }
    if (!address.street.trim()) errors.street = `${prefix} közterület megadása kötelező.`;
    if (!address.houseNumber.trim()) errors.houseNumber = `${prefix} házszám megadása kötelező.`;

    return errors;
  }

  function focusFirstInvalidAddressField(prefix: "shipping" | "billing", errors: AddressFieldErrors) {
    const firstInvalidField = (["postalCode", "city", "street", "houseNumber"] as const).find(
      (field) => Boolean(errors[field]),
    );
    if (!firstInvalidField) return;

    const inputIdByField: Record<typeof firstInvalidField, string> = {
      postalCode: `${prefix}-postal-code`,
      city: `${prefix}-city`,
      street: `${prefix}-street`,
      houseNumber: `${prefix}-house-number`,
    };
    document.getElementById(inputIdByField[firstInvalidField])?.focus();
  }

  function handleShippingChange(nextAddress: AddressFormData) {
    setShippingAddress(nextAddress);
    setShippingErrors({});
    setFormError("");
    setSuccess("");
  }

  function handleBillingChange(nextAddress: AddressFormData) {
    setBillingAddress(nextAddress);
    setBillingErrors({});
    setFormError("");
    setSuccess("");
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError("");
    setSuccess("");
    setShippingErrors({});
    setBillingErrors({});

    const nextShippingErrors = validateAddress(shippingAddress, "shipping");
    if (Object.keys(nextShippingErrors).length > 0) {
      setShippingErrors(nextShippingErrors);
      focusFirstInvalidAddressField("shipping", nextShippingErrors);
      return;
    }

    if (customerType === 1 && !useShippingAsBilling) {
      const nextBillingErrors = validateAddress(billingAddress, "billing");
      if (Object.keys(nextBillingErrors).length > 0) {
        setBillingErrors(nextBillingErrors);
        focusFirstInvalidAddressField("billing", nextBillingErrors);
        return;
      }
    }

    try {
      setIsSaving(true);
      const shouldSaveShippingAddress = hasAddressValue(shippingAddress);
      const shouldSaveBillingAddress = customerType === 1 && !useShippingAsBilling && hasAddressValue(billingAddress);

      await updateAddresses({
        shippingAddress: shouldSaveShippingAddress ? normalizeAddress(shippingAddress) : null,
        billingAddress: shouldSaveBillingAddress ? normalizeAddress(billingAddress) : null,
        useShippingAsBilling: customerType === 1 && useShippingAsBilling && shouldSaveShippingAddress,
      });
      setSuccess(
        getAddressSaveSuccessMessage(
          customerType,
          useShippingAsBilling,
          shouldSaveShippingAddress,
          shouldSaveBillingAddress,
        ),
      );
    } catch (saveError) {
      setFormError(handleAccountError(saveError, "A címek mentése nem sikerült.") ?? "");
    } finally {
      setIsSaving(false);
    }
  }

  if (isLoading) {
    return <AccountLoadingPanel label="Címek betöltése..." />;
  }

  const pageDescription =
    customerType === 0
      ? "Itt tudod elmenteni a rendeléseknél használni kívánt szállítási címet."
      : "Itt tudod elmenteni a rendeléseknél használni kívánt szállítási és számlázási címet.";

  return (
    <AccountPageShell
      title="Mentett címek"
      description={pageDescription}
    >
      <AccountFeedback error={pageError} success="" />

      <form onSubmit={handleSubmit} noValidate className="mt-8 space-y-8">
        <fieldset>
          <legend className="mb-5 text-xl font-bold text-site-heading">Szállítási cím</legend>
          <AddressFields
            idPrefix="shipping"
            value={shippingAddress}
            onChange={handleShippingChange}
            required={false}
            autoCompleteScope="shipping"
            invalidFields={{
              postalCode: Boolean(shippingErrors.postalCode),
              city: Boolean(shippingErrors.city),
              street: Boolean(shippingErrors.street),
              houseNumber: Boolean(shippingErrors.houseNumber),
            }}
            errorMessages={shippingErrors}
          />
        </fieldset>

        {customerType === 0 ? (
          <div className="border-t border-site-border pt-6">
            <p className="rounded-sm bg-site-surface-soft p-4 text-sm leading-6 text-site-text">
              Magánszemélyként nem szükséges számlázási címet megadnod. A rendeléshez papíralapú nyugtát mellékelünk.
            </p>
          </div>
        ) : (
        <div className="border-t border-site-border pt-6">
          <label className="flex items-start gap-3 rounded-sm bg-site-surface-soft p-4 text-sm text-site-text">
            <input
              type="checkbox"
              checked={useShippingAsBilling}
              onChange={(event) => {
                setUseShippingAsBilling(event.target.checked);
                setBillingErrors({});
                setFormError("");
                setSuccess("");
              }}
              className="mt-1 h-4 w-4 accent-site-green"
            />
            <span>A számlázási cím megegyezik a szállítási címmel.</span>
          </label>
        </div>
        )}

        {customerType === 1 && !useShippingAsBilling && (
          <fieldset className="border-t border-site-border pt-7">
            <legend className="mb-5 text-xl font-bold text-site-heading">Számlázási cím</legend>
            <AddressFields
              idPrefix="billing"
              value={billingAddress}
              onChange={handleBillingChange}
              required={false}
              invalidFields={{
                postalCode: Boolean(billingErrors.postalCode),
                city: Boolean(billingErrors.city),
                street: Boolean(billingErrors.street),
                houseNumber: Boolean(billingErrors.houseNumber),
              }}
              errorMessages={billingErrors}
            />
          </fieldset>
        )}

        <div className="flex flex-col gap-3 pt-2 sm:flex-row sm:items-center">
          <button
            type="submit"
            disabled={isSaving}
            className="w-full rounded-sm bg-site-green px-6 py-3 font-semibold text-white transition hover:bg-site-green-hover disabled:cursor-not-allowed disabled:opacity-60 sm:w-auto"
          >
            {isSaving ? "Mentés..." : "Címek mentése"}
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
