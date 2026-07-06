import KnowledgePagination from "./KnowledgePagination";
import type { PostSortOrder } from "../types/post.types";

type FilterAndSortProps = {
  selectedCategory: "all" | 0 | 1;
  sortOrder: PostSortOrder;
  onCategoryChange: (value: "all" | 0 | 1) => void;
  onSortChange: (value: PostSortOrder) => void;
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
    <div className="rounded-sm border border-site-border bg-site-surface-soft p-4 shadow">
      <div className="grid gap-4 lg:grid-cols-[2fr_1fr] lg:items-center">
        {shouldShowPagination && (
          <div className="order-2 flex justify-center lg:order-1 lg:col-start-1">
            <KnowledgePagination
              currentPage={currentPage}
              totalPages={totalPages}
              onPageChange={onPageChange}
              className="mt-0"
            />
          </div>
        )}

        <div className="order-1 flex flex-col gap-3 sm:flex-row lg:order-2 lg:col-start-2 lg:justify-end">
          <select
            id="category"
            aria-label="Bejegyzés kategória"
            value={String(selectedCategory)}
            onChange={(event) => {
              const value = event.target.value;
              onCategoryChange(value === "all" ? "all" : (Number(value) as 0 | 1));
            }}
            className="min-w-0 flex-1 rounded-sm border border-site-border bg-white px-3 py-2 text-site-text focus:border-site-green focus:outline-none focus:ring-2 focus:ring-site-green/15"
          >
            <option value="all">Összes bejegyzés</option>
            <option value="0">Tudástár</option>
            <option value="1">Receptajánló</option>
          </select>

          <select
            id="sortOrder"
            aria-label="Bejegyzések rendezése"
            value={sortOrder}
            onChange={(event) => onSortChange(event.target.value as PostSortOrder)}
            className="min-w-0 flex-1 rounded-sm border border-site-border bg-white px-3 py-2 text-site-text focus:border-site-green focus:outline-none focus:ring-2 focus:ring-site-green/15"
          >
            <option value="newest">Legújabb elöl</option>
            <option value="oldest">Legrégebbi elöl</option>
            <option value="abc">ABC sorrendben</option>
          </select>
        </div>
      </div>
    </div>
  );
}
