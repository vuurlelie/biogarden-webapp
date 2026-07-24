type AdminListEmptyStateProps = {
  title?: string;
  description?: string;
};

export default function AdminListEmptyState({
  title = "Nincs találat",
  description = "Módosítsd a keresési vagy szűrési feltételeket.",
}: AdminListEmptyStateProps) {
  return (
    <section className="rounded-sm border border-site-border bg-white p-8 text-center shadow-sm">
      <h2 className="text-lg font-bold text-site-heading">{title}</h2>
      <p className="mt-2 text-sm text-site-muted">{description}</p>
    </section>
  );
}
