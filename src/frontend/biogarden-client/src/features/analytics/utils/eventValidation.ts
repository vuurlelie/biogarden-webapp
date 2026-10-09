import type {
  AnalyticsEventName,
  AnalyticsPayload,
  AnalyticsPayloadValue,
} from "../types/analytics.types";

const emailPattern = /[^\s@]+@[^\s@]+\.[^\s@]+/i;
const phonePattern = /(?:\+?\d[\s-]*){7,}/;

const allowedPayloadKeys: Record<AnalyticsEventName, readonly string[]> = {
  page_view: ["path"],
  product_view: ["productId", "category"],
  cart_view: ["itemCount", "cartValueBucket"],
  checkout_started: ["itemCount", "cartValueBucket"],
  checkout_step_completed: ["step", "fulfillmentMethod", "paymentMethod"],
  order_submitted: ["paymentMethod", "fulfillmentMethod", "itemCount", "cartValueBucket"],
  payment_return_viewed: ["status"],
  contact_form_submitted: ["source"],
  withdrawal_request_submitted: ["source"],
};

export function getCartValueBucket(value: number): string {
  if (value <= 0)
    return "0";
  if (value < 5_000)
    return "1-4999";
  if (value < 10_000)
    return "5000-9999";
  if (value < 20_000)
    return "10000-19999";
  return "20000+";
}

export function sanitizeAnalyticsPayload(
  eventName: AnalyticsEventName,
  payload: AnalyticsPayload = {},
): AnalyticsPayload {
  const allowedKeys = new Set(allowedPayloadKeys[eventName]);
  const sanitized: AnalyticsPayload = {};

  for (const [key, value] of Object.entries(payload)) {
    if (!allowedKeys.has(key) || !isSafeAnalyticsValue(value))
      continue;
    sanitized[key] = value;
  }

  return sanitized;
}

function isSafeAnalyticsValue(value: AnalyticsPayloadValue | undefined): value is AnalyticsPayloadValue {
  if (value === null || typeof value === "boolean" || typeof value === "number")
    return true;
  if (typeof value !== "string")
    return false;

  return !emailPattern.test(value) && !phonePattern.test(value);
}
