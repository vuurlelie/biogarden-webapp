type AccountFeedbackProps = {
  error: string;
  success: string;
};

export default function AccountFeedback({ error, success }: AccountFeedbackProps) {
  return (
    <>
      {error && (
        <div className="mt-6 rounded-sm bg-site-danger-soft p-3 text-sm text-site-danger" role="alert">
          {error}
        </div>
      )}
      {success && (
        <div
          className="mt-6 rounded-sm border border-site-green/30 bg-site-surface-soft p-3 text-sm text-site-green-dark"
          role="status"
        >
          {success}
        </div>
      )}
    </>
  );
}
