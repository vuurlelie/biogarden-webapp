import type { ContactMessageRequest } from "../types/contact.types";
import { apiRequest } from "../../../shared/api/httpClient";

export async function sendContactMessage(
  request: ContactMessageRequest,
): Promise<void> {
  await apiRequest("/api/contact", {
    fallbackMessage: "Az üzenet küldése nem sikerült. Kérjük, próbáld újra.",
    method: "POST",
    jsonBody: request,
  });
}
