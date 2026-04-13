type KnowledgePaginationProps = {
  currentPage: number;
  totalPages: number;
  onPageChange: (page: number) => void;
};

export default function KnowledgePagination({
  currentPage,
  totalPages,
  onPageChange,
}: KnowledgePaginationProps) {
  if (totalPages <= 1) return null;

  const pages = Array.from({ length: totalPages }, (_, index) => index + 1);

  function changePage(page: number) {
    onPageChange(page);

    requestAnimationFrame(() => {
      window.scrollTo({
        top: 0,
        behavior: "smooth",
      });
    });
  }

  return (
    <div className="mt-8 flex flex-wrap items-center justify-center gap-3">
      <button
        type="button"
        onClick={() => changePage(currentPage - 1)}
        disabled={currentPage === 1}
        className="rounded-xl bg-white px-4 py-2 text-sm text-gray-700 shadow-md transition hover:shadow-lg cursor-pointer disabled:cursor-not-allowed disabled:opacity-50"
      >
        Előző
      </button>

      {pages.map((page) => {
        const isActive = page === currentPage;

        return (
          <button
            key={page}
            type="button"
            onClick={() => changePage(page)}
            className={
              isActive
                ? "rounded-xl bg-[#6F8625] px-4 py-2 text-sm font-semibold text-white shadow-md transition hover:shadow-lg cursor-pointer"
                : "rounded-xl bg-white px-4 py-2 text-sm text-gray-700 shadow-md transition hover:shadow-lg cursor-pointer"
            }
          >
            {page}
          </button>
        );
      })}

      <button
        type="button"
        onClick={() => changePage(currentPage + 1)}
        disabled={currentPage === totalPages}
        className="rounded-lg bg-white px-4 py-2 text-sm text-gray-700 shadow-md transition hover:shadow-lg cursor-pointer disabled:cursor-not-allowed disabled:opacity-50"
      >
        Következő
      </button>
    </div>
  );
}