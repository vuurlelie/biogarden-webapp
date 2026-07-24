type AccountLoadingPanelProps = {
  label: string;
};

export default function AccountLoadingPanel({ label }: AccountLoadingPanelProps) {
  return (
    <div className="rounded-sm bg-site-surface-soft p-10 text-center shadow-md">
      {label}
    </div>
  );
}
