import { getAuthSession } from "../../auth/utils/authStorage";
import type { AdminDashboard } from "../types/dashboard.types";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

type ApiErrorBody = {
  detail?: string;
  Detail?: string;
};

async function getErrorMessage(response: Response): Promise<string> {
  try {
    const body = (await response.json()) as ApiErrorBody;
    return body.detail ?? body.Detail ?? "Az adminisztrációs adatok betöltése nem sikerült.";
  } catch {
    return "Az adminisztrációs adatok betöltése nem sikerült.";
  }
}

export async function getAdminDashboard(signal?: AbortSignal): Promise<AdminDashboard> {
  const auth = getAuthSession();
  if (!auth) throw new Error("A munkamenet lejárt. Jelentkezz be újra.");

  const response = await fetch(`${API_BASE_URL}/api/admin/dashboard`, {
    headers: { Authorization: `Bearer ${auth.token}` },
    signal,
  });

  if (!response.ok) throw new Error(await getErrorMessage(response));
  return response.json() as Promise<AdminDashboard>;
}
