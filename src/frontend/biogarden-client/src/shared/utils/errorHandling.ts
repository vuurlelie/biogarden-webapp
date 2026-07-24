export type ErrorWithStatus = Error & {
  status: number;
};

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
