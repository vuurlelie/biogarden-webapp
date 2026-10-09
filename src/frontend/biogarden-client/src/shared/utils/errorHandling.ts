export type ErrorWithStatus = Error & {
  status: number;
};

export function getServerFieldErrors<T extends string>(
  error: unknown,
  fields: readonly T[],
  aliases: Readonly<Record<string, T>> = {},
): Partial<Record<T, string>> {
  const result: Partial<Record<T, string>> = {};

  if (!(error instanceof Error) || !("fieldErrors" in error) ||
      !error.fieldErrors || typeof error.fieldErrors !== "object")
    {
    return result;
  }

  for (const [key, messages] of Object.entries(error.fieldErrors)) {
    const field = aliases[key] ?? fields.find((candidate) => candidate === key);
    if (field && Array.isArray(messages)) {
      const message = messages.filter((item): item is string => typeof item === "string").join(" ");
      if (message)
        result[field] = message;
    }
  }
  return result;
}

export function getErrorMessage(error: unknown, fallbackMessage: string): string {
  return error instanceof Error ? error.message : fallbackMessage;
}

export function hasErrorStatus(error: unknown, status: number): error is ErrorWithStatus {
  return error instanceof Error &&
    "status" in error &&
    typeof error.status === "number" &&
    error.status === status;
}

export function isAbortError(error: unknown): boolean {
  return error instanceof DOMException && error.name === "AbortError";
}
