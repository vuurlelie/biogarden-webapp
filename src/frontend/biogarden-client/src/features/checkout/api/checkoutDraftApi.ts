import { getAuthSession } from "../../auth/utils/authStorage";
import { apiRequest } from "../../../shared/api/httpClient";
import type { CheckoutDraft } from "../types/checkout.types";
import { EMPTY_DRAFT } from "../utils/checkoutDraft";
import { FULFILLMENT } from "../constants/fulfillment";

export type SavedCheckoutDraft = { version: string | null; expiresAtUtc: string | null; fields: CheckoutDraft | null };

function parseDraft(input: unknown): SavedCheckoutDraft {
  const invalid = () => new Error("A mentett pénztáradatok sérültek.");
  if (!input || typeof input !== "object") throw invalid();
  const value = input as SavedCheckoutDraft;
  if (value.fields === null && value.version === null && value.expiresAtUtc === null) return value;
  if (!value.fields || typeof value.fields !== "object" || typeof value.version !== "string"
    || typeof value.expiresAtUtc !== "string" || !Number.isFinite(Date.parse(value.expiresAtUtc))) throw invalid();
  for (const [key, empty] of Object.entries(EMPTY_DRAFT)) {
    if (key === "fulfillmentMethod" || key === "glsDeliveryPointId") continue;
    if (typeof value.fields[key as keyof CheckoutDraft] !== typeof empty) throw invalid();
  }
  // Older drafts have no shipping selection. Restore them as personal pickup.
  value.fields.fulfillmentMethod ??= EMPTY_DRAFT.fulfillmentMethod;
  value.fields.glsDeliveryPointId ??= null;
  if (!Object.values(FULFILLMENT).includes(value.fields.fulfillmentMethod)
    || (value.fields.glsDeliveryPointId !== null && (typeof value.fields.glsDeliveryPointId !== "string"
      || value.fields.glsDeliveryPointId.length > 100))) throw invalid();
  if (![0, 1].includes(value.fields.customerType)) throw new Error("Érvénytelen vásárlói típus.");
  return { ...value, fields: Object.fromEntries(Object.keys(EMPTY_DRAFT)
    .map(key => [key, value.fields![key as keyof CheckoutDraft]])) as CheckoutDraft };
}

export async function readCheckoutDraft(cartId: string): Promise<SavedCheckoutDraft> {
  const response = await apiRequest(`/api/checkout-drafts/${encodeURIComponent(cartId)}`, {
    authToken: getAuthSession()?.token, credentials: "include", cache: "no-store",
    headers: { "X-Checkout-Request": "1" }, fallbackMessage: "A pénztár piszkozatát nem sikerült betölteni.",
  });
  return parseDraft(await response.json() as SavedCheckoutDraft);
}

export async function saveCheckoutDraft(cartId: string, version: string | null, fields: CheckoutDraft): Promise<SavedCheckoutDraft> {
  const response = await apiRequest(`/api/checkout-drafts/${encodeURIComponent(cartId)}`, {
    method: "PUT", authToken: getAuthSession()?.token, credentials: "include",
    headers: { "X-Checkout-Request": "1" }, jsonBody: { version, fields },
    fallbackMessage: "Az adatok mentése nem sikerült. Ne frissítsd az oldalt a mentésig.",
  });
  return parseDraft(await response.json() as SavedCheckoutDraft);
}

export async function deleteCheckoutDraft(cartId: string, version: string | null): Promise<void> {
  await apiRequest(`/api/checkout-drafts/${encodeURIComponent(cartId)}${version ? `?version=${encodeURIComponent(version)}` : ""}`, {
    method: "DELETE", authToken: getAuthSession()?.token, credentials: "include",
    headers: { "X-Checkout-Request": "1" }, fallbackMessage: "A piszkozat törlése nem sikerült.",
  });
}
