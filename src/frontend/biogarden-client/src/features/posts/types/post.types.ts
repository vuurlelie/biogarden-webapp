export type PostCategory = 0 | 1;
export type PostSortOrder = "newest" | "oldest" | "abc";

export type Post = {
  id: number;
  title: string;
  category: PostCategory;
  article: string;
  image: string;
  publishedAt: string;
  draft: boolean;
};

export type PostDetails = {
  id: number;
  title: string;
  category: PostCategory;
  article: string;
  image: string;
  publishedAt: string;
  draft: boolean;
};
