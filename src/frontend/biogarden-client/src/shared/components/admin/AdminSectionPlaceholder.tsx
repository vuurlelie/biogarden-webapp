import AdminPageHeader from "./AdminPageHeader";

type AdminSectionPlaceholderProps = {
  title: string;
  description: string;
};

export default function AdminSectionPlaceholder({ title, description }: AdminSectionPlaceholderProps) {
  return (
    <div className="space-y-6">
      <AdminPageHeader title={title} description={description} />
      <section className="rounded-sm border border-dashed border-site-border bg-white p-6 text-center shadow-sm sm:p-10">
        <h2 className="text-lg font-bold text-site-heading">A kezelőfelület következő lépésben készül el</h2>
        <p className="mx-auto mt-2 max-w-2xl text-sm leading-6 text-site-muted">
          A navigáció és a reszponzív oldalkeret már használható. Ezt a részt külön, áttekinthető commitban töltjük meg adatokkal és műveletekkel.
        </p>
      </section>
    </div>
  );
}
