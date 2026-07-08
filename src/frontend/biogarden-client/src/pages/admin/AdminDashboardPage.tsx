import AdminPageHeader from "../../shared/components/admin/AdminPageHeader";

export default function AdminDashboardPage() {
  return (
    <div className="space-y-6">
      <AdminPageHeader
        eyebrow="Bakony Kincse"
        title="Adminisztrációs áttekintés"
        description="Innen érheted el a termékek, rendelések, készlet, bejegyzések és felhasználói fiókok kezelését."
      />
      <section className="rounded-sm border border-site-border bg-white p-5 shadow-sm sm:p-6">
        <h2 className="text-lg font-bold text-site-heading">Üdv az adminisztrációs felületen!</h2>
        <p className="mt-2 max-w-3xl text-sm leading-6 text-site-muted">
          Az összesítő adatok és a gyorsműveletek a következő commitban kerülnek erre az oldalra.
        </p>
      </section>
    </div>
  );
}
