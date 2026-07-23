import { NavLink } from "react-router-dom";
import type { Post } from "../types/post.types";
import { ROUTES } from "../../../shared/constants/routes";

type PictureGridProps = {
  posts: Post[];
};

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

export default function PictureGrid({ posts }: PictureGridProps) {
  const displayedPosts = [...posts].reverse().slice(0, 12);

  if (displayedPosts.length === 0) return null;

  return (
    <>
      <section className="mt-10 rounded-sm bg-white p-4 shadow-lg">
        <h3 className="mb-4 text-lg font-bold text-site-heading">
          További bejegyzések
        </h3>

        <div className="grid grid-cols-2 gap-4 md:grid-cols-3">
          {displayedPosts.map((post) => {
            const imageUrl = `${API_BASE_URL}/${post.image}`;

            return (
              <NavLink
                key={post.id}
                to={`${ROUTES.posts}/${post.id}`}
                title={post.title}
                className="group overflow-hidden rounded-sm"
              >
                <img
                  src={imageUrl}
                  alt={post.title}
                  title={post.title}
                  className="aspect-square h-full w-full object-cover shadow-lg transition duration-300 group-hover:scale-105"
                  width={600}
                  height={600}
                  loading="lazy"
                  decoding="async"
                />
              </NavLink>
            );
          })}
        </div>
      </section>

      <section className="rounded-sm bg-white p-4 shadow-lg">
        <h3 className="mb-4 text-lg font-bold text-site-heading">
          További tartalmaink
        </h3>

        <div className="flex items-center gap-4">
          <a
            href="https://www.facebook.com/profile.php?id=100083082863561"
            target="_blank"
            rel="noreferrer"
            className="inline-flex h-11 w-11 items-center justify-center rounded-full bg-[#3b5998] text-white shadow transition hover:brightness-95"
            aria-label="Facebook oldal"
          >
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 320 512" className="h-4 w-4" fill="currentColor">
              <path d="M80 299.3V512H196V299.3h86.5l18-97.8H196V166.9c0-51.7 20.3-71.5 72.7-71.5c16.3 0 29.4 .4 37 1.2V7.9C291.4 4 256.4 0 236.2 0C129.3 0 80 50.5 80 159.4v42.1H14v97.8H80z" />
            </svg>
          </a>

          <a
            href="https://www.instagram.com/bakony.kincse.biokerteszet/"
            target="_blank"
            rel="noreferrer"
            className="inline-flex h-11 w-11 items-center justify-center rounded-full text-white transition hover:brightness-95"
            aria-label="Instagram oldal"
          >
            <svg viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg" className="h-11 w-11">
              <rect x="2" y="2" width="28" height="28" rx="6" fill="url(#sidebar-instagram-base)" />
              <rect x="2" y="2" width="28" height="28" rx="6" fill="url(#sidebar-instagram-warm)" />
              <rect x="2" y="2" width="28" height="28" rx="6" fill="url(#sidebar-instagram-cool)" />
              <path d="M23 10.5C23 11.3284 22.3284 12 21.5 12C20.6716 12 20 11.3284 20 10.5C20 9.67157 20.6716 9 21.5 9C22.3284 9 23 9.67157 23 10.5Z" fill="white" />
              <path fillRule="evenodd" clipRule="evenodd" d="M16 21C18.7614 21 21 18.7614 21 16C21 13.2386 18.7614 11 16 11C13.2386 11 11 13.2386 11 16C11 18.7614 13.2386 21 16 21ZM16 19C17.6569 19 19 17.6569 19 16C19 14.3431 17.6569 13 16 13C14.3431 13 13 14.3431 13 16C13 17.6569 14.3431 19 16 19Z" fill="white" />
              <rect x="7" y="7" width="18" height="18" rx="6" stroke="white" strokeWidth="2" />
              <defs>
                <radialGradient id="sidebar-instagram-base" cx="0" cy="0" r="1" gradientUnits="userSpaceOnUse" gradientTransform="translate(12 23) rotate(-55.3758) scale(25.5196)">
                  <stop stopColor="#B13589" />
                  <stop offset="0.79309" stopColor="#C62F94" />
                  <stop offset="1" stopColor="#8A3AC8" />
                </radialGradient>
                <radialGradient id="sidebar-instagram-warm" cx="0" cy="0" r="1" gradientUnits="userSpaceOnUse" gradientTransform="translate(11 31) rotate(-65.1363) scale(22.5942)">
                  <stop stopColor="#E0E8B7" />
                  <stop offset="0.444662" stopColor="#FB8A2E" />
                  <stop offset="0.71474" stopColor="#E2425C" />
                  <stop offset="1" stopColor="#E2425C" stopOpacity="0" />
                </radialGradient>
                <radialGradient id="sidebar-instagram-cool" cx="0" cy="0" r="1" gradientUnits="userSpaceOnUse" gradientTransform="translate(0.500002 3) rotate(-8.1301) scale(38.8909 8.31836)">
                  <stop offset="0.156701" stopColor="#406ADC" />
                  <stop offset="0.467799" stopColor="#6A45BE" />
                  <stop offset="1" stopColor="#6A45BE" stopOpacity="0" />
                </radialGradient>
              </defs>
            </svg>
          </a>
        </div>
      </section>
    </>
  );
}
