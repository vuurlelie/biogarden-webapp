import { useEffect, useState, type FormEvent } from "react";
import {
  getAddresses,
  getProfile,
  updateAddresses,
} from "../../features/account/api/profileApi";
import AddressFields from "../../features/account/components/AddressFields";
import AccountFeedback from "../../features/account/components/AccountFeedback";
import AccountLoadingPanel from "../../features/account/components/AccountLoadingPanel";
import AccountPageShell from "../../features/account/components/AccountPageShell";
import { useAccountErrorHandler } from "../../features/account/hooks/useAccountErrorHandler";
import type {
  AddressDetails,
  AddressFormData,
} from "../../features/account/types/address.types";

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

export default function AccountAddressesPage() {
  const handleAccountError = useAccountErrorHandler();
  const [customerType, setCustomerType] = useState<0 | 1>(0);
  const [shippingAddress, setShippingAddress] = useState<AddressFormData>({ ...EMPTY_ADDRESS });
  const [billingAddress, setBillingAddress] = useState<AddressFormData>({ ...EMPTY_ADDRESS });
  const [useShippingAsBilling, setUseShippingAsBilling] = useState(true);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

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
        setError(handleAccountError(loadError, "A címek betöltése nem sikerült.") ?? "");
      } finally {
        if (!shouldIgnoreResult) setIsLoading(false);
      }
    }

    void loadAddresses();
    return () => {
      shouldIgnoreResult = true;
    };
  }, [handleAccountError]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError("");
    setSuccess("");

    try {
      setIsSaving(true);
      await updateAddresses({
        shippingAddress: normalizeAddress(shippingAddress),
        billingAddress: customerType === 1 && !useShippingAsBilling ? normalizeAddress(billingAddress) : null,
        useShippingAsBilling: customerType === 1 && useShippingAsBilling,
      });
      setSuccess("A szállítási és számlázási cím mentése sikerült.");
    } catch (saveError) {
      setError(handleAccountError(saveError, "A címek mentése nem sikerült.") ?? "");
    } finally {
      setIsSaving(false);
    }
  }

  if (isLoading) {
    return <AccountLoadingPanel label="Címek betöltése..." />;
  }

  return (
    <AccountPageShell
      title="Mentett címek"
      description="Add meg a rendeléseknél használni kívánt szállítási és számlázási címet."
    >
      <AccountFeedback error={error} success={success} />

      <form onSubmit={handleSubmit} className="mt-8 space-y-8">
        <fieldset>
          <legend className="mb-5 text-xl font-bold text-site-heading">Szállítási cím</legend>
          <AddressFields idPrefix="shipping" value={shippingAddress} onChange={setShippingAddress} />
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
              onChange={(event) => setUseShippingAsBilling(event.target.checked)}
              className="mt-1 h-4 w-4 accent-site-green"
            />
            <span>A számlázási cím megegyezik a szállítási címmel.</span>
          </label>
        </div>
        )}

        {customerType === 1 && !useShippingAsBilling && (
          <fieldset className="border-t border-site-border pt-7">
            <legend className="mb-5 text-xl font-bold text-site-heading">Számlázási cím</legend>
            <AddressFields idPrefix="billing" value={billingAddress} onChange={setBillingAddress} />
          </fieldset>
        )}

        <button
          type="submit"
          disabled={isSaving}
          className="rounded-sm bg-site-green px-6 py-3 font-semibold text-white transition hover:bg-site-green-hover disabled:cursor-not-allowed disabled:opacity-60"
        >
          {isSaving ? "Mentés..." : "Címek mentése"}
        </button>
      </form>
    </AccountPageShell>
  );
}
