import { apiRequest } from "../../../shared/api/httpClient";
import type { GlsPointMethod } from "../constants/fulfillment";

export type GlsDeliveryPoint = {
  id: string;
  type: GlsPointMethod;
  name: string;
  countryCode: string;
  postalCode: string;
  city: string;
  address: string;
  acceptsCash: boolean;
  acceptsCard: boolean;
};

export async function resolveGlsPoint(id: string, type: GlsPointMethod, signal: AbortSignal): Promise<GlsDeliveryPoint> {
  const response = await apiRequest(`/api/shipping/gls/points/${encodeURIComponent(id)}?type=${type}`, {
    cache: "no-store", signal, fallbackMessage: "A GLS átvételi pont ellenőrzése nem sikerült. Próbáld újra, vagy válassz másik átvételi módot.",
  });
  const point = await response.json() as GlsDeliveryPoint;
  if (!point || point.id !== id || point.type !== type || point.countryCode !== "HU"
    || ![point.name, point.postalCode, point.city, point.address].every(value => typeof value === "string" && value.length > 0)
    || typeof point.acceptsCash !== "boolean" || typeof point.acceptsCard !== "boolean") {
    throw new Error("A GLS átvételi pont adatai nem érvényesek. Válassz új pontot.");
  }
  return point;
}
