import { useEffect, useRef, useState } from "react";
import { getAllPosts, getPostPage } from "../../features/posts/api/postsApi";
import Contents from "../../features/posts/components/Contents";
import FilterAndSort from "../../features/posts/components/FilterAndSort";
import KnowledgeCards from "../../features/posts/components/KnowledgeCards";
import KnowledgeNavigation from "../../features/posts/components/KnowledgeNavigation";
import KnowledgePagination from "../../features/posts/components/KnowledgePagination";
import PictureGrid from "../../features/posts/components/PictureGrid";
import type { Post, PostSortOrder } from "../../features/posts/types/post.types";
import Seo from "../../shared/seo/Seo";

const POSTS_PER_PAGE = 4;

export default function PostsPage() {
  const [posts, setPosts] = useState<Post[]>([]);
  const [sidebarPosts, setSidebarPosts] = useState<Post[]>([]);
  const [selectedCategory, setSelectedCategory] = useState<"all" | 0 | 1>("all");
  const [sortOrder, setSortOrder] = useState<PostSortOrder>("newest");
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [isLoading, setIsLoading] = useState(true);
  const [hasError, setHasError] = useState(false);
  const latestLoadId = useRef(0);

  useEffect(() => {
    const loadId = latestLoadId.current + 1;
    latestLoadId.current = loadId;
    const isStaleLoad = () => latestLoadId.current !== loadId;

    async function loadPosts() {
      try {
        setIsLoading(true);
        setHasError(false);

        const result = await getPostPage(
          {
            page: currentPage,
            pageSize: POSTS_PER_PAGE,
            category: selectedCategory,
            sort: sortOrder,
          },
        );

        if (isStaleLoad()) return;

        setPosts(result.items);
        setTotalPages(result.totalPages);
        setTotalCount(result.totalCount);
      } catch (error) {
        if (isStaleLoad()) return;

        console.error(error);
        setHasError(true);
      } finally {
        if (!isStaleLoad()) {
          setIsLoading(false);
        }
      }
    }

    void loadPosts();
  }, [selectedCategory, sortOrder, currentPage]);

  useEffect(() => {
    let isCurrentRequest = true;

    async function loadSidebarPosts() {
      try {
        const allPosts = await getAllPosts();

        if (isCurrentRequest) {
          setSidebarPosts(allPosts);
        }
      } catch (error) {
        if (isCurrentRequest) {
          console.error(error);
        }
      }
    }

    void loadSidebarPosts();

    return () => {
      isCurrentRequest = false;
    };
  }, []);

  function resetToFirstPage() {
    setCurrentPage(1);
  }

  function handleCategoryChange(value: "all" | 0 | 1) {
    resetToFirstPage();
    setSelectedCategory(value);
  }

  function handleSortChange(value: PostSortOrder) {
    resetToFirstPage();
    setSortOrder(value);
  }

  function handlePageChange(page: number) {
    setCurrentPage(page);

    requestAnimationFrame(() => {
      document.getElementById("posts-top")?.scrollIntoView({
        behavior: "smooth",
        block: "start",
      });
    });
  }

  if (isLoading) {
    return (
      <div className="mx-auto max-w-6xl px-4 py-12">
        <h1 className="text-3xl font-bold text-site-heading">Tudástár</h1>
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
        <h1 className="text-3xl font-bold text-site-heading">Tudástár</h1>
      <KnowledgeNavigation />
        <div className="mt-8 rounded-2xl bg-site-danger-soft p-8 text-center text-site-danger shadow-lg">
          Hiba történt a bejegyzések betöltése közben.
        </div>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-6xl px-4 py-4">
      <Seo
        title="Tudástár"
        description="Cikkek, gondolatok és hasznos tudnivalók a gyógynövényekről, természetes termékekről és a Bakony Kincse Biokertészet világából."
      />
      <h1 className="text-3xl font-bold text-site-heading">Tudástár</h1>
      <KnowledgeNavigation />

      <div id="posts-top" className="mt-4 scroll-mt-20">
        <FilterAndSort
          selectedCategory={selectedCategory}
          sortOrder={sortOrder}
          onCategoryChange={handleCategoryChange}
          onSortChange={handleSortChange}
          currentPage={currentPage}
          totalPages={totalPages}
          onPageChange={handlePageChange}
        />
      </div>

      <div className="mt-3 text-sm text-site-text">
        {totalCount > 0
          ? `${(currentPage - 1) * POSTS_PER_PAGE + 1}-${Math.min(
            currentPage * POSTS_PER_PAGE,
            totalCount,
          )} bejegyzés, összesen: ${totalCount} db`
          : "Nincs a szűrésnek megfelelő bejegyzés."}
      </div>

      <div className="mt-8 grid gap-8 lg:grid-cols-[2fr_1fr]">
        <div>
          {posts.length > 0 ? (
            <>
              <KnowledgeCards posts={posts} />

              <KnowledgePagination
                currentPage={currentPage}
                totalPages={totalPages}
                onPageChange={handlePageChange}
              />
            </>
          ) : (
            <div className="rounded-2xl bg-white p-8 text-center shadow-lg">
              Nincs a szűrésnek megfelelő bejegyzés.
            </div>
          )}
        </div>

        <div className="space-y-8">
          <Contents posts={sidebarPosts.length > 0 ? sidebarPosts : posts} />
          <PictureGrid posts={sidebarPosts.length > 0 ? sidebarPosts : posts} />
        </div>
      </div>
    </div>
  );
}
