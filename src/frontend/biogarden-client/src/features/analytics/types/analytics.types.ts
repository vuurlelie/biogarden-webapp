export type ConsentPreferences = {
  necessary: true;
  analytics: boolean;
};

export type StoredConsentPreferences = ConsentPreferences & {
  version: number;
  updatedAtUtc: string;
};

export type AnalyticsEventName =
  | "page_view"
  | "product_view"
  | "cart_view"
  | "checkout_started"
  | "checkout_step_completed"
  | "order_submitted"
  | "payment_return_viewed"
  | "contact_form_submitted"
  | "withdrawal_request_submitted";

export type AnalyticsPayloadValue = string | number | boolean | null;

export type AnalyticsPayload = Partial<Record<string, AnalyticsPayloadValue>>;

export type AnalyticsEvent = {
  name: AnalyticsEventName;
  payload: AnalyticsPayload;
  occurredAtUtc: string;
};
