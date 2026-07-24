type AdminFeedbackMessagesProps = {
  message: string | null;
  error: string | null;
  retryLabel?: string;
  onRetry?: () => void;
};

export default function AdminFeedbackMessages({
  message,
  error,
  retryLabel = "Újrapróbálás",
  onRetry,
}: AdminFeedbackMessagesProps) {
  return (
    <>
      {message && (
        <div
          role="status"
          className="rounded-sm border border-site-green/25 bg-site-surface-soft p-4 font-semibold text-site-green-dark"
        >
          {message}
        </div>
      )}

      {error && (
        <div
          role="alert"
          className="flex flex-wrap items-center justify-between gap-3 rounded-sm bg-site-danger-soft p-4 text-site-danger"
        >
          <span>{error}</span>
          {onRetry && (
            <button
              type="button"
              onClick={onRetry}
              className="rounded-sm border border-site-danger px-3 py-2 text-sm font-semibold"
            >
              {retryLabel}
            </button>
          )}
        </div>
      )}
    </>
  );
}
