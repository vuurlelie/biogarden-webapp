import { adminListQuery, type AdminListQuery } from "./adminListQuery";
import type { PagedResult } from "../../../shared/types/pagination";
import type { UnitOfMeasure } from "../../products/types/product.types";
import { adminRequest } from "./adminApiClient";

export type AdminInventoryItem = {
  version: string;
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

export async function getAdminInventory(query: AdminListQuery, signal?: AbortSignal): Promise<PagedResult<AdminInventoryItem>> {
  const response = await adminRequest(`/inventory${adminListQuery(query)}`, {
    signal,
  });
  return response.json() as Promise<PagedResult<AdminInventoryItem>>;
}

export async function updateAdminInventory(
  productId: number,
  stockQuantity: number,
  available: boolean,
  version: string,
): Promise<AdminInventoryItem> {
  const response = await adminRequest(`/inventory/${productId}`, {
    method: "PUT",
    jsonBody: { stockQuantity, available, version },
  });
  return response.json() as Promise<AdminInventoryItem>;
}

export async function getAdminInventoryItem(id: number): Promise<AdminInventoryItem> {
  const response = await adminRequest(`/inventory/${id}`);
  return response.json() as Promise<AdminInventoryItem>;
}
