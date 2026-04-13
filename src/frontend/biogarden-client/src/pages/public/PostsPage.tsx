import { useEffect, useMemo, useState } from "react";
import KnowledgeNavigation from "../../features/posts/components/KnowledgeNavigation";
import FilterAndSort from "../../features/posts/components/FilterAndSort";
import KnowledgeCards from "../../features/posts/components/KnowledgeCards";
import KnowledgePagination from "../../features/posts/components/KnowledgePagination";
import PictureGrid from "../../features/posts/components/PictureGrid";
import Contents from "../../features/posts/components/Contents";
import type { Post } from "../../features/posts/types/post.types";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

const POSTS_PER_PAGE = 4;

export default function PostsPage() {
  const [posts, setPosts] = useState<Post[]>([]);
  const [selectedCategory, setSelectedCategory] = useState<"all" | 0 | 1>("all");
  const [sortOrder, setSortOrder] = useState<"newest" | "oldest">("newest");
  const [currentPage, setCurrentPage] = useState(1);

  const [isLoading, setIsLoading] = useState(true);
  const [hasError, setHasError] = useState(false);

    function handlePageChange(page: number) {
    setCurrentPage(page);

    requestAnimationFrame(() => {
      const target = document.getElementById("posts-top");

      if (target) {
        target.scrollIntoView({
          behavior: "smooth",
          block: "start",
        });
        return;
      }

      window.scrollTo({
        top: 0,
        behavior: "smooth",
      });
    });
  }
  
  useEffect(() => {
    async function loadPosts() {
      try {
        setIsLoading(true);
        setHasError(false);

        const url = `${API_BASE_URL}/api/posts`;
        console.log("Fetching posts from:", url);

        const response = await fetch(url);

        if (!response.ok) {
          throw new Error("Failed to fetch posts.");
        }

        const data: Post[] = await response.json();
        setPosts(data);
      } catch (error) {
        console.error(error);
        setHasError(true);
      } finally {
        setIsLoading(false);
      }
    }

    loadPosts();
  }, []);

  const publicPosts = useMemo(() => {
    return posts.filter((post) => !post.draft);
  }, [posts]);

  const filteredPosts = useMemo(() => {
    if (selectedCategory === "all") {
      return publicPosts;
    }

    return publicPosts.filter((post) => post.category === selectedCategory);
  }, [publicPosts, selectedCategory]);

  const sortedPosts = useMemo(() => {
    return [...filteredPosts].sort((a, b) => {
      const first = new Date(a.publishedAt).getTime();
      const second = new Date(b.publishedAt).getTime();

      return sortOrder === "newest" ? second - first : first - second;
    });
  }, [filteredPosts, sortOrder]);

  const totalPages = Math.ceil(sortedPosts.length / POSTS_PER_PAGE);

  const paginatedPosts = useMemo(() => {
    const start = (currentPage - 1) * POSTS_PER_PAGE;
    const end = start + POSTS_PER_PAGE;
    return sortedPosts.slice(start, end);
  }, [sortedPosts, currentPage]);

  useEffect(() => {
    setCurrentPage(1);
  }, [selectedCategory, sortOrder]);

  if (isLoading) {
    return (
      <div className="mx-auto max-w-6xl px-4 py-12">
        <KnowledgeNavigation />
        <div className="mt-8 rounded-2xl bg-white p-8 text-center shadow-lg">
          Bejegyzések betöltése...
        </div>
      </div>
    );
  }

  if (hasError) {
    return (
      <div className="mx-auto max-w-6xl px-4 py-12">
        <KnowledgeNavigation />
        <div className="mt-8 rounded-2xl bg-white p-8 text-center text-red-600 shadow-lg">
          Hiba történt a bejegyzések betöltése közben.
        </div>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-6xl px-4 py-4">
      <KnowledgeNavigation />

      <div className="mt-4">
        <FilterAndSort
        selectedCategory={selectedCategory}
        sortOrder={sortOrder}
        onCategoryChange={setSelectedCategory}
        onSortChange={setSortOrder}
        currentPage={currentPage}
        totalPages={totalPages}
        onPageChange={handlePageChange}
      />
      </div>

      <div className="mt-8 grid gap-8 lg:grid-cols-[2fr_1fr]">
        <div>
          {paginatedPosts.length > 0 ? (
            <>
              <KnowledgeCards posts={paginatedPosts} />

              <KnowledgePagination
                currentPage={currentPage}
                totalPages={totalPages}
                onPageChange={setCurrentPage}
              />
            </>
          ) : (
            <div className="rounded-2xl bg-white p-8 text-center shadow-lg">
              Nincs a szűrésnek megfelelő bejegyzés.
            </div>
          )}
        </div>

        <div className="space-y-8">
          <Contents posts={sortedPosts} />
          <PictureGrid posts={sortedPosts} />
        </div>
      </div>
    </div>
  );
}