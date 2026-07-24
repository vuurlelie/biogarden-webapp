import { useId, useRef } from "react";
import { useModalFocus } from "../../hooks/useModalFocus";

type AdminDeleteConfirmationModalProps = {
  title: string;
  itemName: string;
  description: string;
  warning: string;
  deleting: boolean;
  confirmLabel?: string;
  cancelLabel?: string;
  deletingLabel?: string;
  onCancel: () => void;
  onConfirm: () => void;
};

export default function AdminDeleteConfirmationModal({
  title,
  itemName,
  description,
  warning,
  deleting,
  confirmLabel = "Törlés",
  cancelLabel = "Mégse",
  deletingLabel = "Törlés...",
  onCancel,
  onConfirm,
}: AdminDeleteConfirmationModalProps) {
  const dialogRef = useRef<HTMLElement | null>(null);
  const titleId = useId();

  useModalFocus(dialogRef, {
    isOpen: true,
    onClose: () => {
      if (!deleting) onCancel();
    },
  });

  return (
    <div
      className="fixed inset-0 z-[90] grid place-items-center bg-black/55 p-4"
      role="dialog"
      aria-modal="true"
      aria-labelledby={titleId}
    >
      <section
        ref={dialogRef}
        tabIndex={-1}
        className="w-full max-w-md rounded-sm bg-white p-6 shadow-xl outline-none"
      >
        <h2 id={titleId} className="text-xl font-bold text-site-heading">
          {title}
        </h2>
        <p className="mt-3 leading-6 text-site-muted">
          {description} <strong className="text-site-heading">{itemName}</strong>?
        </p>
        <p className="mt-2 text-sm text-site-danger">{warning}</p>
        <div className="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
          <button
            type="button"
            onClick={onCancel}
            className="rounded-sm border border-site-border px-4 py-3 font-semibold"
          >
            {cancelLabel}
          </button>
          <button
            type="button"
            disabled={deleting}
            onClick={onConfirm}
            className="rounded-sm bg-site-danger px-4 py-3 font-semibold text-white disabled:opacity-60"
          >
            {deleting ? deletingLabel : confirmLabel}
          </button>
        </div>
      </section>
    </div>
  );
}
