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
    <div className="rounded-sm bg-white p-4 shadow">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div className="flex flex-col gap-2">
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
            className="rounded-lg border border-site-border bg-white px-4 py-2 text-site-text focus:border-site-green focus:outline-none focus:ring-2 focus:ring-site-green/15"
          >
            <option value="all">Összes bejegyzés</option>
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
              className="mt-0"
            />
          </div>
        )}

        <div className="flex flex-col gap-2">
          <select
            id="sortOrder"
            value={sortOrder}
            onChange={(e) => onSortChange(e.target.value as PostSortOrder)}
            className="rounded-lg border border-site-border bg-white px-4 py-2 text-site-text focus:border-site-green focus:outline-none focus:ring-2 focus:ring-site-green/15"
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
