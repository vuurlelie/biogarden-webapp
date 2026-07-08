import { getAuthSession } from "../../auth/utils/authStorage";
import type { UnitOfMeasure } from "../../products/types/product.types";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;
export type AdminInventoryItem = { productId: number; name: string; image: string; unit: UnitOfMeasure; stockQuantity: number; reservedQuantity: number; availableQuantity: number; available: boolean; deliveryAvailable: boolean };

function headers(): HeadersInit { const auth = getAuthSession(); if (!auth) throw new Error("A munkamenet lejárt. Jelentkezz be újra."); return { Authorization: `Bearer ${auth.token}` }; }
async function ensure(response: Response) { if (response.ok) return; try { const body = await response.json() as { detail?: string; Detail?: string }; throw new Error(body.detail ?? body.Detail ?? "A művelet nem sikerült."); } catch (error) { if (error instanceof Error && error.message !== "Unexpected end of JSON input") throw error; throw new Error("A művelet nem sikerült."); } }

export async function getAdminInventory(signal?: AbortSignal): Promise<AdminInventoryItem[]> { const response = await fetch(`${API_BASE_URL}/api/admin/inventory`, { headers: headers(), signal }); await ensure(response); return response.json() as Promise<AdminInventoryItem[]>; }
export async function updateAdminInventory(productId: number, stockQuantity: number, available: boolean): Promise<AdminInventoryItem> { const response = await fetch(`${API_BASE_URL}/api/admin/inventory/${productId}`, { method: "PUT", headers: { ...headers(), "Content-Type": "application/json" }, body: JSON.stringify({ stockQuantity, available }) }); await ensure(response); return response.json() as Promise<AdminInventoryItem>; }
