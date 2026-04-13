import KnowledgePagination from "./KnowledgePagination";

type FilterAndSortProps = {
  selectedCategory: "all" | 0 | 1;
  sortOrder: "newest" | "oldest";
  onCategoryChange: (value: "all" | 0 | 1) => void;
  onSortChange: (value: "newest" | "oldest") => void;

  currentPage?: number;
  totalPages?: number;
  onPageChange?: (page: number) => void;
};

export default function FilterAndSort({
  selectedCategory,
  sortOrder,
  onCategoryChange,
  onSortChange,
  currentPage,
  totalPages,
  onPageChange,
}: FilterAndSortProps) {
  const shouldShowPagination =
    typeof currentPage === "number" &&
    typeof totalPages === "number" &&
    typeof onPageChange === "function" &&
    totalPages > 1;

  return (
    <div className="rounded-sm bg-white p-4 shadow">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div className="flex flex-col gap-2">
          <label htmlFor="category" className="text-sm font-semibold text-gray-700">
            Kategória
          </label>
          <select
            id="category"
            value={String(selectedCategory)}
            onChange={(e) => {
              const value = e.target.value;
              if (value === "all") {
                onCategoryChange("all");
              } else {
                onCategoryChange(Number(value) as 0 | 1);
              }
            }}
            className="rounded-lg border border-gray-300 px-4 py-2 text-gray-700 focus:border-[#6F8625] focus:outline-none"
          >
            <option value="all">Összes</option>
            <option value="0">Tudástár</option>
            <option value="1">Receptajánló</option>
          </select>
        </div>

        {shouldShowPagination && (
          <div className="flex-1 lg:px-6">
            <KnowledgePagination
              currentPage={currentPage}
              totalPages={totalPages}
              onPageChange={onPageChange}
            />
          </div>
        )}

        <div className="flex flex-col gap-2">
          <label htmlFor="sortOrder" className="text-sm font-semibold text-gray-700">
            Rendezés
          </label>
          <select
            id="sortOrder"
            value={sortOrder}
            onChange={(e) => onSortChange(e.target.value as "newest" | "oldest")}
            className="rounded-lg border border-gray-300 px-4 py-2 text-gray-700 focus:border-[#6F8625] focus:outline-none"
          >
            <option value="newest">Legújabb elöl</option>
            <option value="oldest">Legrégebbi elöl</option>
          </select>
        </div>
      </div>
    </div>
  );
}