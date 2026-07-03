import { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import type { PostDetails } from "../../features/posts/types/post.types";
import PostDetailsContent from "../../features/posts/components/PostDetailsContent";
import NotFoundPage from "./NotFoundPage";

const API_BASE_URL = "http://localhost:5097";

export default function PostDetailsPage() {
  const { postId } = useParams<{ postId: string }>();

  const [post, setPost] = useState<PostDetails | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [hasError, setHasError] = useState(false);
  const [notFound, setNotFound] = useState(false);

  useEffect(() => {
    async function loadPost() {
      if (!postId) {
        setNotFound(true);
        setIsLoading(false);
        return;
      }

      try {
        setIsLoading(true);
        setHasError(false);
        setNotFound(false);

        const response = await fetch(`${API_BASE_URL}/api/posts/${postId}`);

        if (response.status === 404) {
          setNotFound(true);
          setPost(null);
          return;
        }

        if (!response.ok) {
          throw new Error("Failed to fetch post details.");
        }

        const data: PostDetails = await response.json();
        setPost(data);
      } catch (error) {
        console.error(error);
        setHasError(true);
      } finally {
        setIsLoading(false);
      }
    }

    loadPost();
  }, [postId]);

  if (isLoading) {
    return (
      <div className="mx-auto max-w-6xl px-4 py-12">
        <div className="rounded-2xl bg-white p-8 text-center shadow-lg">
          Bejegyzés betöltése...
        </div>
      </div>
    );
  }

  if (notFound) {
    return (
      <NotFoundPage message="A keresett azonosítójú bejegyzés sajnos nem található." />
    );
  }

  if (hasError || !post) {
    return (
      <div className="mx-auto max-w-6xl px-4 py-12">
        <div className="rounded-2xl bg-white p-8 text-center text-red-600 shadow-lg">
          Hiba történt a bejegyzés betöltése közben.
        </div>
      </div>
    );
  }

  return <PostDetailsContent post={post} />;
}