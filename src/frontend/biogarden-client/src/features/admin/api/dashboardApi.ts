import type { AdminDashboard } from "../types/dashboard.types";
import { adminRequest } from "./adminApiClient";

export async function getAdminDashboard(signal?: AbortSignal): Promise<AdminDashboard> {
  const response = await adminRequest("/dashboard", {
    fallbackMessage: "Az adminisztrációs adatok betöltése nem sikerült.",
    signal,
  });
  return response.json() as Promise<AdminDashboard>;
}
