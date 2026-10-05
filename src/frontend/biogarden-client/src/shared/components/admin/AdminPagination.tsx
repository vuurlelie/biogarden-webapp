type AdminPaginationProps = {
  page: number;
  totalPages: number;
  onPageChange: (page: number) => void;
};

export default function AdminPagination({ page, totalPages, onPageChange }: AdminPaginationProps) {
  if (totalPages <= 1) return null;
  const pages = [...new Set([1, page - 1, page, page + 1, totalPages])]
    .filter(candidate => candidate >= 1 && candidate <= totalPages).sort((a, b) => a - b);

  return (
    <nav aria-label="Lapozás" className="flex flex-wrap items-center justify-center gap-2">
      <button type="button" disabled={page === 1} onClick={() => onPageChange(page - 1)} className="rounded-sm border border-site-border bg-white px-4 py-2.5 text-sm font-semibold text-site-nav hover:bg-site-surface-soft disabled:cursor-not-allowed disabled:opacity-40">Előző</button>
      {pages.map((candidate, index) => <span key={candidate} className="contents">{index > 0 && candidate - pages[index - 1] > 1 && <span className="px-1 text-site-muted">…</span>}<button type="button" aria-current={candidate === page ? "page" : undefined} onClick={() => onPageChange(candidate)} className={`min-w-11 rounded-sm border px-3 py-2.5 text-sm font-semibold ${candidate === page ? "border-site-green bg-site-green text-white" : "border-site-border bg-white text-site-nav hover:bg-site-surface-soft"}`}>{candidate}</button></span>)}
      <button type="button" disabled={page === totalPages} onClick={() => onPageChange(page + 1)} className="rounded-sm border border-site-border bg-white px-4 py-2.5 text-sm font-semibold text-site-nav hover:bg-site-surface-soft disabled:cursor-not-allowed disabled:opacity-40">Következő</button>
    </nav>
  );
}
