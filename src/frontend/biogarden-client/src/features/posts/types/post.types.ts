export type PostCategory = 0 | 1;

export type Post = {
  id: number;
  title: string;
  article: string;
  image: string;
  category: PostCategory;
  publishedAt: string;
  draft: boolean;
};