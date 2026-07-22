import type { ContactMessageRequest } from "../types/contact.types";
import { getApiErrorMessage } from "../../../shared/api/httpClient";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

async function getErrorMessage(response: Response): Promise<string> {
  return getApiErrorMessage(
    response,
    "Az üzenet küldése nem sikerült. Kérjük, próbáld újra.",
  );
}

export async function sendContactMessage(
  request: ContactMessageRequest,
): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/contact`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    throw new Error(await getErrorMessage(response));
  }
}
