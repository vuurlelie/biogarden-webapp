import type { UnitOfMeasure } from "../../products/types/product.types";
import { adminRequest } from "./adminApiClient";

export type AdminInventoryItem = {
  productId: number;
  name: string;
  image: string;
  unit: UnitOfMeasure;
  stockQuantity: number;
  reservedQuantity: number;
  availableQuantity: number;
  available: boolean;
  deliveryAvailable: boolean;
};

export async function getAdminInventory(signal?: AbortSignal): Promise<AdminInventoryItem[]> {
  const response = await adminRequest("/inventory", {
    signal,
  });
  return response.json() as Promise<AdminInventoryItem[]>;
}

export async function updateAdminInventory(
  productId: number,
  stockQuantity: number,
  available: boolean,
): Promise<AdminInventoryItem> {
  const response = await adminRequest(`/inventory/${productId}`, {
    method: "PUT",
    jsonBody: { stockQuantity, available },
  });
  return response.json() as Promise<AdminInventoryItem>;
}
