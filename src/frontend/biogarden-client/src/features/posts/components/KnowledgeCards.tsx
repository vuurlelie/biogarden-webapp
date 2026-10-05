import { managedImage } from "../../../shared/utils/managedImage";
import { NavLink } from "react-router";
import type { Post } from "../types/post.types";
import { ROUTES } from "../../../shared/constants/routes";
import FacebookPostActions from "./FacebookPostActions";


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
        const postPath = `${ROUTES.posts}/${post.id}`;

        return (
          <div key={post.id}>
            <article className="rounded-sm bg-white shadow-sm transition hover:shadow-xl">
              <NavLink to={postPath} className="block">
                
                {/* KÉP KERETTEL */}
                <div className="p-4">
                  <img
                    className="h-52 w-full object-cover rounded-sm md:h-64"
                    {...managedImage(post.image)}
                    sizes="(min-width: 1024px) 750px, 100vw"
                    alt={post.title}
                    width={1600}
                    height={1200}
                    loading="lazy"
                    decoding="async"
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
              </NavLink>

              <div className="border-t border-site-border px-6 py-4">
                <FacebookPostActions compact postPath={postPath} title={post.title} />
              </div>
            </article>
          </div>
        );
      })}
    </div>
  );
}
