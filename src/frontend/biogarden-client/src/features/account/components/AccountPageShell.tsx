import type { ReactNode } from "react";

type AccountPageShellProps = {
  title: string;
  description: string;
  children: ReactNode;
};

export default function AccountPageShell({
  title,
  description,
  children,
}: AccountPageShellProps) {
  return (
    <div className="rounded-sm border border-site-border bg-white p-6 shadow-lg sm:p-8">
      <p className="text-xs font-bold uppercase tracking-[0.15em] text-site-green-dark">Fiókadatok</p>
      <h1 className="mt-2 text-3xl font-bold text-site-heading">{title}</h1>
      <p className="mt-2 text-sm leading-6 text-site-text">
        {description}
      </p>

      {children}
    </div>
  );
}
