import { getAuthSession } from "../../auth/utils/authStorage";
import type { Post, PostCategory } from "../../posts/types/post.types";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

export type AdminPostInput = {
  title: string;
  category: PostCategory;
  article: string;
  image: string | null;
  publishedAt: string;
  draft: boolean;
};

function authorizationHeaders(): HeadersInit {
  const auth = getAuthSession();
  if (!auth) throw new Error("A munkamenet lejárt. Jelentkezz be újra.");
  return { Authorization: `Bearer ${auth.token}` };
}

async function errorMessage(response: Response): Promise<string> {
  try {
    const body = await response.json() as { detail?: string; Detail?: string; errors?: Record<string, string[]> };
    const validationMessage = body.errors ? Object.values(body.errors).flat()[0] : null;
    return validationMessage ?? body.detail ?? body.Detail ?? "A művelet nem sikerült.";
  } catch {
    return "A művelet nem sikerült.";
  }
}

async function ensureSuccess(response: Response): Promise<void> {
  if (!response.ok) throw new Error(await errorMessage(response));
}

export async function getAdminPosts(signal?: AbortSignal): Promise<Post[]> {
  const response = await fetch(`${API_BASE_URL}/api/admin/posts`, {
    headers: authorizationHeaders(),
    signal,
  });
  await ensureSuccess(response);
  return response.json() as Promise<Post[]>;
}

export async function createAdminPost(post: AdminPostInput): Promise<number> {
  const response = await fetch(`${API_BASE_URL}/api/admin/posts`, {
    method: "POST",
    headers: { ...authorizationHeaders(), "Content-Type": "application/json" },
    body: JSON.stringify(post),
  });
  await ensureSuccess(response);
  return response.json() as Promise<number>;
}

export async function updateAdminPost(postId: number, post: AdminPostInput): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/admin/posts/${postId}`, {
    method: "PUT",
    headers: { ...authorizationHeaders(), "Content-Type": "application/json" },
    body: JSON.stringify(post),
  });
  await ensureSuccess(response);
}

export async function deleteAdminPost(postId: number): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/admin/posts/${postId}`, {
    method: "DELETE",
    headers: authorizationHeaders(),
  });
  await ensureSuccess(response);
}

export async function uploadAdminPostImage(image: File): Promise<string> {
  const data = new FormData();
  data.append("image", image);

  const response = await fetch(`${API_BASE_URL}/api/admin/posts/image`, {
    method: "POST",
    headers: authorizationHeaders(),
    body: data,
  });
  await ensureSuccess(response);

  const result = await response.json() as { path: string };
  return result.path;
}

export async function deleteUnusedAdminPostImage(path: string): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/admin/posts/image`, {
    method: "DELETE",
    headers: { ...authorizationHeaders(), "Content-Type": "application/json" },
    body: JSON.stringify({ path }),
  });
  await ensureSuccess(response);
}
