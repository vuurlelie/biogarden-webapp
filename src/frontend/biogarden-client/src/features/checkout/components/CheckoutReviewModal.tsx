import type { RefObject } from "react";
import { Link } from "react-router-dom";
import type { Cart } from "../../cart/types/cart.types";
import { ROUTES } from "../../../shared/constants/routes";
import type { CheckoutDraft, CheckoutFieldErrors, FulfillmentMethod, PaymentMethod } from "../types/checkout.types";
import { paymentLabel, PRICE } from "../utils/checkoutFormatting";
import { SummaryCartItem } from "./SummaryCartItem";

type CheckoutReviewModalProps = {
  cart: Cart;
  draft: CheckoutDraft;
  effectiveCustomerType: 0 | 1;
  fieldErrors: CheckoutFieldErrors;
  grandTotal: number;
  method: FulfillmentMethod;
  onClose: () => void;
  onFieldErrorsChange: (updater: (current: CheckoutFieldErrors) => CheckoutFieldErrors) => void;
  onPlaceOrder: () => void;
  onPrivacyAcceptedChange: (accepted: boolean) => void;
  onTermsAcceptedChange: (accepted: boolean) => void;
  paymentMethod: PaymentMethod | null;
  privacyAccepted: boolean;
  reviewModalRef: RefObject<HTMLElement | null>;
  submitting: boolean;
  termsAccepted: boolean;
};

export function CheckoutReviewModal({
  cart,
  draft,
  effectiveCustomerType,
  fieldErrors,
  grandTotal,
  method,
  onClose,
  onFieldErrorsChange,
  onPlaceOrder,
  onPrivacyAcceptedChange,
  onTermsAcceptedChange,
  paymentMethod,
  privacyAccepted,
  reviewModalRef,
  submitting,
  termsAccepted,
}: CheckoutReviewModalProps) {
  return (
    <div
      className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-black/50 p-3 pb-44 pt-24 sm:pt-28"
      role="dialog"
      aria-modal="true"
      aria-labelledby="review-title"
    >
      <section
        ref={reviewModalRef}
        tabIndex={-1}
        className="max-h-[calc(100vh-7rem)] w-full max-w-5xl overflow-y-auto rounded-sm bg-white p-4 shadow-xl outline-none sm:max-h-[calc(100vh-8rem)] sm:p-5"
      >
        <div className="flex items-start justify-between gap-4">
          <h2 id="review-title" className="text-2xl font-bold text-site-heading">
            Megrendelés áttekintése
          </h2>
          <button
            type="button"
            onClick={onClose}
            aria-label="Bezárás"
            className="text-2xl"
          >
            ×
          </button>
        </div>

        <div className="mt-4 space-y-3 text-sm">
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            <div className="rounded-sm bg-site-surface-soft p-3">
              <strong className="block">Kapcsolattartó</strong>
              <p>{draft.customerName}</p>
              <p>{draft.customerEmail}</p>
              <p>{draft.customerPhone}</p>
            </div>

            <div className="space-y-3">
              <div className="rounded-sm bg-site-surface-soft p-3">
                <strong className="block">Rendelési mód</strong>
                <p>
                  {method === 0
                    ? "Személyes átvétel"
                    : method === 1
                      ? "Házhoz szállítás"
                      : "Csomagautomatába szállítás"}
                </p>
              </div>
              <div className="rounded-sm bg-site-surface-soft p-3">
                <strong className="block">Fizetés</strong>
                <p>{paymentLabel(paymentMethod)}</p>
              </div>
            </div>

            <div className="space-y-3">
              <div className="rounded-sm bg-site-surface-soft p-3">
                <strong className="block">Számlázási cím</strong>
                {effectiveCustomerType === 1 && draft.billingCompanyName && (
                  <p>
                    {draft.billingCompanyName} · {draft.billingTaxNumber}
                  </p>
                )}
                <p>
                  {effectiveCustomerType === 1
                    ? `${draft.postalCode} ${draft.city}, ${draft.street} ${draft.houseNumber}`
                    : "Papíralapú nyugta a rendelés mellé."}
                </p>
                {effectiveCustomerType === 1 && (
                  <p>
                    {draft.printedInvoiceRequested
                      ? "Elektronikus és nyomtatott számla"
                      : "Elektronikus számla"}
                  </p>
                )}
              </div>
              <div
                className={`rounded-sm p-3 ${method !== 0 ? "bg-site-surface-soft" : "invisible"}`}
                aria-hidden={method === 0}
              >
                <strong className="block">Szállítási cím</strong>
                {method !== 0 && (
                  <p>
                    {`${draft.deliveryPostalCode} ${draft.deliveryCity}, ${draft.deliveryStreet} ${draft.deliveryHouseNumber}`}
                  </p>
                )}
              </div>
            </div>
          </div>

          <div className="border-y border-site-border py-3">
            <ul className="divide-y divide-site-border">
              {cart.items.map((item) => (
                <SummaryCartItem key={item.id} item={item} compact />
              ))}
            </ul>
          </div>

          <p className="flex justify-between text-lg font-bold">
            <span>Összesen</span>
            <span>{PRICE.format(grandTotal)}</span>
          </p>

          <div className="space-y-3">
            <div>
              <label className="flex items-start gap-3">
                <input
                  type="checkbox"
                  checked={termsAccepted}
                  onChange={(event) => {
                    onTermsAcceptedChange(event.target.checked);
                    onFieldErrorsChange((current) => ({ ...current, terms: undefined }));
                  }}
                  className="mt-1"
                />
                <span>
                  Elolvastam és elfogadom az{" "}
                  <Link to={ROUTES.terms} className="font-semibold text-site-green-dark underline">
                    Általános Szerződési Feltételeket
                  </Link>
                  . <span className="text-site-danger">*</span>
                </span>
              </label>
              {fieldErrors.terms && (
                <p className="mt-1 text-xs text-site-danger">{fieldErrors.terms}</p>
              )}
            </div>

            <div>
              <label className="flex items-start gap-3">
                <input
                  type="checkbox"
                  checked={privacyAccepted}
                  onChange={(event) => {
                    onPrivacyAcceptedChange(event.target.checked);
                    onFieldErrorsChange((current) => ({ ...current, privacy: undefined }));
                  }}
                  className="mt-1"
                />
                <span>
                  Elolvastam és elfogadom az{" "}
                  <Link to={ROUTES.privacyPolicy} className="font-semibold text-site-green-dark underline">
                    Adatkezelési Tájékoztatót
                  </Link>
                  . <span className="text-site-danger">*</span>
                </span>
              </label>
              {fieldErrors.privacy && (
                <p className="mt-1 text-xs text-site-danger">{fieldErrors.privacy}</p>
              )}
            </div>
          </div>
        </div>

        <div className="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
          <button
            type="button"
            onClick={onClose}
            className="rounded-sm border border-site-border px-5 py-3"
          >
            Vissza a szerkesztéshez
          </button>
          <button
            type="button"
            onClick={onPlaceOrder}
            disabled={submitting}
            className="rounded-sm bg-site-green px-5 py-3 font-semibold text-white disabled:opacity-50"
          >
            {submitting
              ? paymentMethod === "online-card"
                ? "Átirányítás..."
                : "Rendelés elküldése..."
              : paymentMethod === "online-card"
                ? "Tovább a fizetéshez"
                : "Megrendelés elküldése"}
          </button>
        </div>
      </section>
    </div>
  );
}
