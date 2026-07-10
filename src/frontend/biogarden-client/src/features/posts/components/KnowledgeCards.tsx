import { NavLink } from "react-router-dom";
import type { Post } from "../types/post.types";
import { ROUTES } from "../../../shared/constants/routes";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

type KnowledgeCardsProps = {
  posts: Post[];
};

function truncateHtmlContent(html: string, maxLength = 260) {
  const parsed = new DOMParser().parseFromString(html, "text/html");
  const plainText = (parsed.body.textContent ?? "").replace(/\s+/g, " ").trim();

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
          <div key={post.id}>
            <NavLink to={`${ROUTES.posts}/${post.id}`} className="block">
              <article className="rounded-sm bg-white shadow-sm transition hover:shadow-xl">
                
                {/* KÉP KERETTEL */}
                <div className="p-4">
                  <img
                    className="h-52 w-full object-cover rounded-sm md:h-64"
                    src={imageUrl}
                    alt={post.title}
                    loading="lazy"
                  />
                </div>

                {/* SZÖVEG */}
                <div className="px-6 pb-6">
                  <div className="pb-3 text-sm font-bold uppercase tracking-wide text-site-green-dark">
                    {getPostCategoryLabel(post.category)}
                  </div>

                  <div className="pb-4 text-2xl font-bold text-site-heading transition hover:text-site-green-dark md:text-3xl">
                    {post.title}
                  </div>

                  <div className="pb-3 text-sm text-site-text">
                    Szerző:{" "}
                    <span className="font-semibold text-site-heading">
                      Volf Gábor
                    </span>
                    , Közzététel:{" "}
                    {new Date(post.publishedAt).toLocaleString("hu-HU", {
                      dateStyle: "short",
                      timeStyle: "short",
                    })}
                  </div>

                  <div className="pb-5 text-justify text-site-text">
                    <div className="line-clamp-3 leading-7">
                      {truncateHtmlContent(post.article)}
                    </div>
                  </div>

                  <div className="font-bold uppercase text-site-green transition hover:text-site-green-hover">
                    Tovább →
                  </div>
                </div>
              </article>
            </NavLink>
          </div>
        );
      })}
    </div>
  );
}
