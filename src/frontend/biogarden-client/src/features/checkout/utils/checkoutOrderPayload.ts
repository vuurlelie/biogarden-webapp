import type { CreateOrderRequest } from "../../account/api/ordersApi";
import { LEGAL_DOCUMENT_VERSIONS } from "../../../shared/constants/legalDocuments";
import type { CheckoutDraft, FulfillmentMethod } from "../types/checkout.types";
import { FULFILLMENT, isGlsMethod } from "../constants/fulfillment";
import type { CheckoutQuote } from "../api/checkoutPricingApi";

export function buildCheckoutOrderRequest(draft: CheckoutDraft, effectiveCustomerType: 0 | 1, method: FulfillmentMethod, cartId: string, quote: CheckoutQuote): CreateOrderRequest {
  return {
    idempotencyKey: cartId.replaceAll("-", ""),
    fulfillmentMethod: method,
    paymentMethod: quote.paymentMethod,
    quoteVersion: quote.version,
    glsDeliveryPointId: isGlsMethod(method) ? draft.glsDeliveryPointId : null,
    customerName: draft.customerName,
    customerEmail: draft.customerEmail,
    customerPhone: draft.customerPhone,
    customerType: effectiveCustomerType,
    addressId: null,
    postalCode: effectiveCustomerType === 1 ? draft.postalCode : "",
    city: effectiveCustomerType === 1 ? draft.city : "",
    street: effectiveCustomerType === 1 ? draft.street : "",
    houseNumber: effectiveCustomerType === 1 ? draft.houseNumber : "",
    building: effectiveCustomerType === 1 ? draft.building : "",
    floor: effectiveCustomerType === 1 ? draft.floor : "",
    door: effectiveCustomerType === 1 ? draft.door : "",
    doorbell: effectiveCustomerType === 1 ? draft.doorbell : "",
    billingCompanyName: effectiveCustomerType === 1 ? draft.billingCompanyName : "",
    billingTaxNumber: effectiveCustomerType === 1 ? draft.billingTaxNumber : "",
    printedInvoiceRequested: effectiveCustomerType === 1 && draft.printedInvoiceRequested,
    acceptedTermsVersion: LEGAL_DOCUMENT_VERSIONS.terms,
    acceptedPrivacyPolicyVersion: LEGAL_DOCUMENT_VERSIONS.privacyPolicy,
    deliveryAddressId: null,
    ...(method === FULFILLMENT.HomeDelivery
      ? {
          deliveryPostalCode: draft.deliveryPostalCode,
          deliveryCity: draft.deliveryCity,
          deliveryStreet: draft.deliveryStreet,
          deliveryHouseNumber: draft.deliveryHouseNumber,
          deliveryBuilding: draft.deliveryBuilding,
          deliveryFloor: draft.deliveryFloor,
          deliveryDoor: draft.deliveryDoor,
          deliveryDoorbell: draft.deliveryDoorbell,
        }
      : {}),
  };
}
