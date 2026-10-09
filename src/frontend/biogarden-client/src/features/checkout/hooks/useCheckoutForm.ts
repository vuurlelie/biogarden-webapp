import { useEffect, useState } from "react";
import { useNavigate } from "react-router";
import { getAddresses, getProfile } from "../../account/api/profileApi";
import type { UserAddresses } from "../../account/types/address.types";
import type { UserProfile } from "../../account/types/profile.types";
import { getAuthSession } from "../../auth/utils/authStorage";
import { useCheckoutDraft } from "./useCheckoutDraft";
import { FULFILLMENT, isGlsMethod } from "../constants/fulfillment";
import type { CheckoutDraftTextField, CheckoutFieldErrors, FulfillmentMethod } from "../types/checkout.types";
import { billingAddressDraft, buildCustomerName, deliveryAddressDraft, emptyBillingDraft, emptyCustomerDraft, emptyDeliveryDraft } from "../utils/checkoutDraft";
import { isCheckoutFieldValid } from "../utils/checkoutValidation";
import { ROUTES } from "../../../shared/constants/routes";
import type { AddressFormData } from "../../../shared/types/address.types";
import { getErrorMessage, hasErrorStatus } from "../../../shared/utils/errorHandling";

export function useCheckoutForm(cartId: string | undefined) {
  const navigate = useNavigate();
  const authenticated = Boolean(getAuthSession());
  const [addresses, setAddresses] = useState<UserAddresses | null>(null);
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [contactManual, setContactManual] = useState(!authenticated);
  const [billingManual, setBillingManual] = useState(!authenticated);
  const [deliveryManual, setDeliveryManual] = useState(!authenticated);
  const draftStorage = useCheckoutDraft(cartId);
  const { draft, setDraft, ready: draftReady } = draftStorage;
  const method = draft.fulfillmentMethod;
  const [loading, setLoading] = useState(authenticated);
  const [error, setError] = useState("");
  const [fieldErrors, setFieldErrors] = useState<CheckoutFieldErrors>({});
  const hasDraft = Object.values(draft).some(Boolean);
  const effectiveCustomerType = authenticated && profile ? profile.customerType : draft.customerType;
  const billingAddress: AddressFormData = {
    postalCode: draft.postalCode,
    city: draft.city,
    street: draft.street,
    houseNumber: draft.houseNumber,
    building: draft.building,
    floor: draft.floor,
    door: draft.door,
    doorbell: draft.doorbell,
  };
  const deliveryAddress: AddressFormData = {
    postalCode: draft.deliveryPostalCode,
    city: draft.deliveryCity,
    street: draft.deliveryStreet,
    houseNumber: draft.deliveryHouseNumber,
    building: draft.deliveryBuilding,
    floor: draft.deliveryFloor,
    door: draft.deliveryDoor,
    doorbell: draft.deliveryDoorbell,
  };
  useEffect(() => {
    if (!authenticated || profile) return;
    if (!draftReady) return;

    let ignore = false;

    void Promise.all([getAddresses(), getProfile()])
      .then(([loadedAddresses, loadedProfile]) => {
        if (ignore) return;

        setAddresses(loadedAddresses);
        setProfile(loadedProfile);

        if (!hasDraft) {
          setContactManual(false);
          setBillingManual(!loadedAddresses.billingAddress);
          setDeliveryManual(!loadedAddresses.shippingAddress);
          setDraft((current) => ({
            ...current,
            customerName: buildCustomerName(loadedProfile),
            customerEmail: loadedProfile.email ?? "",
            customerPhone: loadedProfile.phoneNumber ?? "",
            customerType: loadedProfile.customerType,
            billingCompanyName: loadedProfile.companyName ?? "",
            billingTaxNumber: loadedProfile.taxNumber ?? "",
            printedInvoiceRequested: loadedProfile.printedInvoiceRequested,
            ...(loadedAddresses.billingAddress ? billingAddressDraft(loadedAddresses.billingAddress) : {}),
            ...(loadedAddresses.shippingAddress ? deliveryAddressDraft(loadedAddresses.shippingAddress) : {}),
          }));
        } else {
          // Restored values are a checkout snapshot, not a live selection of profile data.
          setContactManual(true);
          setBillingManual(true);
          setDeliveryManual(true);
        }
      })
      .catch((loadError) => {
        if (!ignore) {
          if (hasErrorStatus(loadError, 401)) {
            navigate(ROUTES.login, { replace: true });
            return;
          }
          setError(getErrorMessage(loadError, "A fiókadatok betöltése nem sikerült."));
        }
      })
      .finally(() => {
        if (!ignore) setLoading(false);
      });

    return () => {
      ignore = true;
    };
  }, [authenticated, hasDraft, navigate, profile, draftReady, setDraft]);

  function setMethod(next: FulfillmentMethod) {
    setDraft(current => ({ ...current, fulfillmentMethod: next, glsDeliveryPointId: null,
      ...(isGlsMethod(next) ? emptyDeliveryDraft()
        : next === FULFILLMENT.HomeDelivery && !deliveryManual && addresses?.shippingAddress
          ? deliveryAddressDraft(addresses.shippingAddress) : {}) }));
    setFieldErrors({});
  }

  function update(field: CheckoutDraftTextField, value: string) {
    setDraft((current) => ({ ...current, [field]: value }));
    setFieldErrors((current) => ({ ...current, [field]: undefined }));
  }

  function fieldClass(field: CheckoutDraftTextField): string {
    const base = "mt-2 block w-full rounded-sm border px-3 py-2.5 disabled:cursor-default";
    if (fieldErrors[field]) return `${base} border-site-danger bg-white`;

    return isCheckoutFieldValid(field, draft)
      ? `${base} border-site-green bg-site-surface-soft`
      : `${base} border-site-border bg-white`;
  }

  function clearFieldErrors(fields: Array<keyof CheckoutFieldErrors>) {
    setFieldErrors((current) => {
      const next = { ...current };
      fields.forEach((field) => {
        next[field] = undefined;
      });
      return next;
    });
  }

  function handleBillingAddressChange(address: AddressFormData) {
    setBillingManual(true);
    clearFieldErrors(["postalCode", "city", "street", "houseNumber", "billingAddress"]);
    setDraft((current) => ({
      ...current,
      postalCode: address.postalCode,
      city: address.city,
      street: address.street,
      houseNumber: address.houseNumber,
      building: address.building ?? "",
      floor: address.floor ?? "",
      door: address.door ?? "",
      doorbell: address.doorbell ?? "",
    }));
  }

  function handleDeliveryAddressChange(address: AddressFormData) {
    setDeliveryManual(true);
    clearFieldErrors([
      "deliveryPostalCode",
      "deliveryCity",
      "deliveryStreet",
      "deliveryHouseNumber",
      "deliveryAddress",
    ]);
    setDraft((current) => ({
      ...current,
      deliveryPostalCode: address.postalCode,
      deliveryCity: address.city,
      deliveryStreet: address.street,
      deliveryHouseNumber: address.houseNumber,
      deliveryBuilding: address.building ?? "",
      deliveryFloor: address.floor ?? "",
      deliveryDoor: address.door ?? "",
      deliveryDoorbell: address.doorbell ?? "",
    }));
  }

  function handleAccountContactChange(checked: boolean) {
    setContactManual(!checked);
    clearFieldErrors(["customerName", "customerEmail", "customerPhone", "accountContact"]);
    setDraft((current) => (
      checked && profile
        ? {
            ...current,
            customerName: buildCustomerName(profile),
            customerEmail: profile.email ?? "",
            customerPhone: profile.phoneNumber ?? "",
          }
        : { ...current, ...emptyCustomerDraft() }
    ));
  }

  function handleAccountBillingChange(checked: boolean) {
    setBillingManual(!checked);
    clearFieldErrors([
      "billingCompanyName",
      "billingTaxNumber",
      "postalCode",
      "city",
      "street",
      "houseNumber",
      "billingAddress",
    ]);

    const address = addresses?.billingAddress;
    setDraft((current) => (
      checked && address
        ? {
            ...current,
            billingCompanyName: profile?.companyName ?? "",
            billingTaxNumber: profile?.taxNumber ?? "",
            ...billingAddressDraft(address),
          }
        : { ...current, ...emptyBillingDraft() }
    ));
  }

  function handleAccountDeliveryChange(checked: boolean) {
    setDeliveryManual(!checked);
    clearFieldErrors([
      "deliveryPostalCode",
      "deliveryCity",
      "deliveryStreet",
      "deliveryHouseNumber",
      "deliveryAddress",
    ]);

    const address = addresses?.shippingAddress;
    setDraft((current) => (
      checked && address
        ? { ...current, ...deliveryAddressDraft(address) }
        : { ...current, ...emptyDeliveryDraft() }
    ));
  }

  return { draftStorage, draft, setDraft, draftReady, method, authenticated, addresses,
    contactManual, billingManual, deliveryManual, loading: authenticated && !profile && loading,
    error, setError, fieldErrors, setFieldErrors,
    effectiveCustomerType, billingAddress, deliveryAddress, setMethod, update, fieldClass, clearFieldErrors,
    handleBillingAddressChange, handleDeliveryAddressChange, handleAccountContactChange,
    handleAccountBillingChange, handleAccountDeliveryChange };
}
