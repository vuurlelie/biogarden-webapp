export const FULFILLMENT = {
  PersonalPickup: 0,
  HomeDelivery: 1,
  ParcelLocker: 2,
  ParcelShop: 3,
} as const;

export type FulfillmentMethod = typeof FULFILLMENT[keyof typeof FULFILLMENT];
export type GlsPointMethod = typeof FULFILLMENT.ParcelLocker | typeof FULFILLMENT.ParcelShop;
export const isGlsMethod = (method: FulfillmentMethod): method is GlsPointMethod =>
  method === FULFILLMENT.ParcelLocker || method === FULFILLMENT.ParcelShop;

export const FULFILLMENT_LABELS: Record<FulfillmentMethod, string> = {
  [FULFILLMENT.PersonalPickup]: "Személyes átvétel",
  [FULFILLMENT.HomeDelivery]: "Házhoz szállítás",
  [FULFILLMENT.ParcelLocker]: "GLS Csomagautomata",
  [FULFILLMENT.ParcelShop]: "GLS CsomagPont",
};
