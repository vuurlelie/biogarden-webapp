import type {
  CheckoutDraft,
  CheckoutDraftTextField,
  CheckoutFieldErrors,
  FulfillmentMethod,
} from "../types/checkout.types";
import { isPostalCodeCityPairValid } from "../../../shared/utils/postalCodeLookup";

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const POSTAL_CODE_PATTERN = /^\d{4}$/;
const TAX_NUMBER_PATTERN = /^\d{8}(-\d-\d{2})?$/;

export function isCheckoutFieldValid(field: CheckoutDraftTextField, draft: CheckoutDraft): boolean {
  const value = draft[field].trim();

  return field === "customerName" ? value.length >= 2
    : field === "customerEmail" ? EMAIL_PATTERN.test(value)
    : field === "customerPhone" ? value.replace(/\D/g, "").length >= 9
    : field === "postalCode" || field === "deliveryPostalCode" ? POSTAL_CODE_PATTERN.test(value)
    : field === "city" || field === "street" || field === "deliveryCity" || field === "deliveryStreet"
      ? value.length >= 2
    : field === "houseNumber" || field === "deliveryHouseNumber" ? value.length > 0
    : field === "billingCompanyName"
      ? value.length > 0 && TAX_NUMBER_PATTERN.test(draft.billingTaxNumber.trim())
    : field === "billingTaxNumber"
      ? TAX_NUMBER_PATTERN.test(value) && draft.billingCompanyName.trim().length > 0
    : value.length > 0;
}

export function validateCheckoutFields(
  draft: CheckoutDraft,
  effectiveCustomerType: 0 | 1,
  method: FulfillmentMethod,
): CheckoutFieldErrors {
  const errors: CheckoutFieldErrors = {};

  if (draft.customerName.trim().length < 2) {
    errors.customerName = "A név megadása kötelező, legalább 2 karakterrel.";
  }
  if (!EMAIL_PATTERN.test(draft.customerEmail.trim())) {
    errors.customerEmail = "Adj meg egy érvényes email címet.";
  }
  if (draft.customerPhone.replace(/\D/g, "").length < 9) {
    errors.customerPhone = "Adj meg egy érvényes telefonszámot.";
  }

  if (effectiveCustomerType === 1) {
    if (!POSTAL_CODE_PATTERN.test(draft.postalCode.trim())) {
      errors.postalCode = "Az irányítószám 4 számjegyből álljon.";
    }
    if (draft.city.trim().length < 2) {
      errors.city = "A település megadása kötelező.";
    } else if (!errors.postalCode && !isPostalCodeCityPairValid(draft.postalCode, draft.city)) {
      errors.city = "A település nem tartozik ehhez az irányítószámhoz.";
    }
    if (draft.street.trim().length < 2) {
      errors.street = "A közterület megadása kötelező.";
    }
    if (!draft.houseNumber.trim()) {
      errors.houseNumber = "A házszám megadása kötelező.";
    }

    const hasCompanyName = Boolean(draft.billingCompanyName.trim());
    const hasTaxNumber = Boolean(draft.billingTaxNumber.trim());
    if (hasCompanyName !== hasTaxNumber) {
      errors.billingCompanyName = "Céges számlázáshoz a cégnév és az adószám együtt szükséges.";
      errors.billingTaxNumber = "Céges számlázáshoz a cégnév és az adószám együtt szükséges.";
    } else if (hasTaxNumber && !TAX_NUMBER_PATTERN.test(draft.billingTaxNumber.trim())) {
      errors.billingTaxNumber = "Hibás adószám. Példa: 12345678-1-12.";
    }
  }

  if (method !== 0) {
    if (!POSTAL_CODE_PATTERN.test(draft.deliveryPostalCode.trim())) {
      errors.deliveryPostalCode = "Az irányítószám 4 számjegyből álljon.";
    }
    if (draft.deliveryCity.trim().length < 2) {
      errors.deliveryCity = "A település megadása kötelező.";
    } else if (
      !errors.deliveryPostalCode
      && !isPostalCodeCityPairValid(draft.deliveryPostalCode, draft.deliveryCity)
    ) {
      errors.deliveryCity = "A település nem tartozik ehhez az irányítószámhoz.";
    }
    if (draft.deliveryStreet.trim().length < 2) {
      errors.deliveryStreet = "A közterület megadása kötelező.";
    }
    if (!draft.deliveryHouseNumber.trim()) {
      errors.deliveryHouseNumber = "A házszám megadása kötelező.";
    }
  }

  return errors;
}
