type ProductPaginationProps = {
  currentPage: number;
  totalPages: number;
  onPageChange: (page: number) => void;
};

export default function ProductPagination({
  currentPage,
  totalPages,
  onPageChange,
}: ProductPaginationProps) {
  if (totalPages <= 1) return null;

  const pages = Array.from({ length: totalPages }, (_, index) => index + 1);

  return (
    <nav
      className="mt-8 flex flex-wrap items-center justify-center gap-2"
      aria-label="Termékoldalak"
    >
      <button
        type="button"
        disabled={currentPage === 1}
        onClick={() => onPageChange(currentPage - 1)}
        className="rounded-sm bg-white px-4 py-2 text-sm text-site-text shadow transition hover:shadow-md disabled:cursor-not-allowed disabled:opacity-40"
      >
        Előző
      </button>

      {pages.map((page) => (
        <button
          key={page}
          type="button"
          onClick={() => onPageChange(page)}
          aria-current={page === currentPage ? "page" : undefined}
          className={
            page === currentPage
              ? "rounded-sm bg-site-green px-4 py-2 text-sm font-semibold text-white shadow"
              : "rounded-sm bg-white px-4 py-2 text-sm text-site-text shadow transition hover:shadow-md"
          }
        >
          {page}
        </button>
      ))}

      <button
        type="button"
        disabled={currentPage === totalPages}
        onClick={() => onPageChange(currentPage + 1)}
        className="rounded-sm bg-white px-4 py-2 text-sm text-site-text shadow transition hover:shadow-md disabled:cursor-not-allowed disabled:opacity-40"
      >
        Következő
      </button>
    </nav>
  );
}
