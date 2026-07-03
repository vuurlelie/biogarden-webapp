import { NavLink } from "react-router-dom";
import type { Post } from "../types/post.types";
import { ROUTES } from "../../../shared/constants/routes";

type PictureGridProps = {
  posts: Post[];
};

const API_BASE_URL = "http://localhost:5097";

export default function PictureGrid({ posts }: PictureGridProps) {
  const displayedPosts = [...posts].reverse().slice(0, 12);

  if (displayedPosts.length === 0) return null;

  return (
    <section className="mt-10 rounded-sm bg-white p-4 shadow-lg">
      <h3 className="mb-4 text-lg font-bold text-site-heading">
        Friss bejegyzések
      </h3>

      <div className="grid grid-cols-2 gap-4 md:grid-cols-3">
        {displayedPosts.map((post) => {
          const imageUrl = `${API_BASE_URL}/${post.image}`;

          return (
            <NavLink
              key={post.id}
              to={`${ROUTES.posts}/${post.id}`}
              className="group overflow-hidden rounded-sm"
            >
              <img
                src={imageUrl}
                alt={post.title}
                className="h-full w-full object-cover shadow-lg transition duration-300 group-hover:scale-105"
                loading="lazy"
              />
            </NavLink>
          );
        })}
      </div>
    </section>
  );
}
