import { useMemo, useState } from "react";
import { NavLink } from "react-router-dom";
import type { Post } from "../types/post.types";
import { ROUTES } from "../../../shared/constants/routes";

type ContentsProps = {
  posts: Post[];
};

function groupPostsByYearAndMonth(posts: Post[]) {
  const grouped: Record<string, Record<string, Post[]>> = {};

  posts.forEach((post) => {
    const date = new Date(post.publishedAt);
    const year = date.getFullYear().toString();
    const month = date.toLocaleString("hu-HU", { month: "long" });

    if (!grouped[year]) grouped[year] = {};
    if (!grouped[year][month]) grouped[year][month] = [];

    grouped[year][month].push(post);
  });

  return grouped;
}

export default function Contents({ posts }: ContentsProps) {
  const groupedPosts = useMemo(() => groupPostsByYearAndMonth(posts), [posts]);
  const years = useMemo(
    () => Object.keys(groupedPosts).sort((a, b) => Number(b) - Number(a)),
    [groupedPosts]
  );

  const [openYears, setOpenYears] = useState<Record<string, boolean>>(() =>
    Object.fromEntries(years.map((year) => [year, true]))
  );

  const [openMonths, setOpenMonths] = useState<Record<string, boolean>>({});

  if (posts.length === 0) return null;

  function toggleYear(year: string) {
    setOpenYears((prev) => ({
      ...prev,
      [year]: !(prev[year] ?? true),
    }));
  }

  function toggleMonth(year: string, month: string) {
    const key = `${year}-${month}`;

    setOpenMonths((prev) => ({
      ...prev,
      [key]: !(prev[key] ?? false),
    }));
  }

  return (
    <aside className="rounded-sm bg-white p-5 shadow-lg">
      <h3 className="mb-4 text-lg font-bold text-site-heading">Tartalomjegyzék</h3>

      <div className="max-h-[70vh] overflow-y-auto pr-1">
        <div className="space-y-4">
          {years.map((year) => (
            <div key={year}>
              <button
                type="button"
                onClick={() => toggleYear(year)}
                className="flex w-full items-center justify-between rounded-md py-1 text-left font-semibold text-site-green hover:bg-gray-50"
              >
                <span>{year}</span>
                <span className="text-sm">{openYears[year] ? "▾" : "▸"}</span>
              </button>

              {openYears[year] && (
                <div className="mt-2 space-y-3">
                  {Object.entries(groupedPosts[year]).map(([month, monthPosts]) => {
                    const monthKey = `${year}-${month}`;
                    const isOpen = openMonths[monthKey] ?? false;

                    return (
                      <div key={monthKey}>
                        <button
                          type="button"
                          onClick={() => toggleMonth(year, month)}
                          className="flex w-full items-center justify-between rounded-md py-1 text-left text-sm font-medium text-site-text hover:bg-gray-50"
                        >
                          <span>{month}</span>
                          <span className="text-xs">{isOpen ? "▾" : "▸"}</span>
                        </button>

                        {isOpen && (
                          <div className="ml-3 mt-1 space-y-1">
                            {monthPosts.map((post) => (
                              <NavLink
                                key={post.id}
                                to={`${ROUTES.posts}/${post.id}`}
                                className="block text-sm text-site-text hover:text-site-green hover:underline"
                              >
                                {post.title}
                              </NavLink>
                            ))}
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          ))}
        </div>
      </div>
    </aside>
  );
}
