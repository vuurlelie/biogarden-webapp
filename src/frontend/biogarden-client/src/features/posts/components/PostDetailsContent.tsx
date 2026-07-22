import parse from "html-react-parser";
import type { PostDetails } from "../types/post.types";
import PostDetailsNavigation from "./PostDetailsNavigation";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

type PostDetailsContentProps = {
  post: PostDetails;
};

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

export default function PostDetailsContent({
  post,
}: PostDetailsContentProps) {
  const imageUrl = `${API_BASE_URL}/${post.image}`;

  return (
    <div className="pb-12">
      <div className="mx-auto max-w-6xl px-4 py-4">
        <PostDetailsNavigation title={post.title} />
      </div>

      <div className="mx-auto max-w-6xl px-4">
        <div className="mb-4 text-center">
          <p className="text-sm font-bold text-site-green-dark md:text-base">
            {new Date(post.publishedAt).toLocaleString("hu-HU", {
              dateStyle: "short",
              timeStyle: "short",
            })}
            <span className="px-2 text-site-green-dark">/</span>
            <span className="uppercase text-site-green-dark">
              {getPostCategoryLabel(post.category)}
            </span>
          </p>

          <h1 className="mt-3 break-words text-3xl font-bold text-site-heading md:text-4xl lg:text-5xl">
            {post.title}
          </h1>
        </div>

        <div className="relative overflow-hidden rounded-sm shadow-lg">
          <img
            src={imageUrl}
            alt={post.title}
            className="h-[260px] w-full object-cover md:h-[420px] lg:h-[520px]"
            width={1600}
            height={1200}
            decoding="async"
          />
        </div>

        <div className="relative mx-auto max-w-5xl px-2">
          <div className="relative z-10 -mt-[220px] md:-mt-[350px] lg:-mt-[450px]">
            <div className="rounded-sm bg-white/95 p-6 text-sm leading-7 text-site-text shadow-xl md:p-10 md:text-base">
              <div className="prose prose-neutral max-w-none prose-img:rounded-xl prose-headings:text-site-heading prose-p:text-site-text prose-p:leading-7">
                {parse(post.article)}
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
