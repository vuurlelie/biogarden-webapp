import type { ReactNode } from "react";

type AdminPageHeaderProps = {
  eyebrow?: string;
  title: string;
  description: string;
  actions?: ReactNode;
};

export default function AdminPageHeader({
  eyebrow = "Adminisztráció",
  title,
  description,
  actions,
}: AdminPageHeaderProps) {
  return (
    <header className="rounded-sm border border-site-border bg-white p-5 shadow-sm sm:p-6 lg:p-8">
      <div className="flex flex-col gap-5 sm:flex-row sm:items-end sm:justify-between">
        <div className="max-w-3xl">
          <p className="text-xs font-bold uppercase tracking-[0.18em] text-site-green-dark">{eyebrow}</p>
          <h1 className="mt-2 text-2xl font-bold text-site-heading sm:text-3xl">{title}</h1>
          <p className="mt-2 text-sm leading-6 text-site-muted sm:text-base">{description}</p>
        </div>
        {actions && <div className="flex shrink-0 flex-wrap gap-3">{actions}</div>}
      </div>
    </header>
  );
}
