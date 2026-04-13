import parse from "html-react-parser";
import { NavLink } from "react-router-dom";
import type { Post } from "../types/post.types";
import { ROUTES } from "../../../shared/constants/routes";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

type KnowledgeCardsProps = {
  posts: Post[];
};

function truncateHtmlContent(html: string, maxLength = 220) {
  const plainText = html.replace(/<[^>]+>/g, "");
  return plainText.length > maxLength
    ? `${plainText.slice(0, maxLength).trim()}...`
    : plainText;
}

function getPostCategoryLabel(category: 0 | 1): string {
  switch (category) {
    case 0:
      return "Tudástár";
    case 1:
      return "Receptajánló";
    default:
      return "Ismeretlen";
  }
}

export default function KnowledgeCards({ posts }: KnowledgeCardsProps) {
  return (
    <div className="grid gap-8">
      {posts.map((post) => {
        const imageUrl = `${API_BASE_URL}/${post.image}`;

        return (
          <article
            key={post.id}
            className="overflow-hidden rounded-sm bg-white shadow-lg transition hover:shadow-xl"
          >
            <NavLink to={`${ROUTES.posts}/${post.id}`}>
              <img
                src={imageUrl}
                alt={post.title}
                className="h-64 w-full object-cover"
                loading="lazy"
              />
            </NavLink>

            <div className="p-6">
              <div className="mb-3 text-sm font-semibold uppercase tracking-wide text-[#6F8625]">
                {getPostCategoryLabel(post.category)} /{" "}
                {new Date(post.publishedAt).toLocaleDateString("hu-HU", {
                  year: "numeric",
                  month: "long",
                  day: "numeric",
                })}
              </div>

              <NavLink to={`${ROUTES.posts}/${post.id}`}>
                <h2 className="mb-4 text-2xl font-bold text-gray-800 hover:text-[#6F8625]">
                  {post.title}
                </h2>
              </NavLink>

              <div className="mb-5 text-justify leading-7 text-gray-700">
                {parse(truncateHtmlContent(post.article))}
              </div>

              <NavLink
                to={`${ROUTES.posts}/${post.id}`}
                className="inline-flex items-center font-semibold text-[#6F8625] hover:underline"
              >
                Tovább olvasom
              </NavLink>
            </div>
          </article>
        );
      })}
    </div>
  );
}