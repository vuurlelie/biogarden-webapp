export const TOO_MANY_REQUESTS_MESSAGE =
  "Túl sok kérés érkezett. Kérjük, várj egy percet, majd próbáld újra.";

type ApiErrorBody = {
  message?: string;
  title?: string;
  detail?: string;
  Detail?: string;
  errors?: Record<string, string[]>;
};

export async function getApiErrorMessage(
  response: Response,
  fallbackMessage: string,
): Promise<string> {
  if (response.status === 429) {
    return TOO_MANY_REQUESTS_MESSAGE;
  }

  try {
    const body = (await response.json()) as ApiErrorBody;
    const validationMessage = body.errors
      ? Object.values(body.errors).flat().join(" ")
      : undefined;

    return body.message
      ?? body.detail
      ?? body.Detail
      ?? validationMessage
      ?? body.title
      ?? fallbackMessage;
  } catch {
    return fallbackMessage;
  }
}
