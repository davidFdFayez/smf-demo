import { httpClient } from "./httpClient";

export type NewsCategory =
  | "Announcement"
  | "Championship"
  | "Education"
  | "PressRelease"
  | "General";

export interface NewsArticleSummary {
  id: string;
  title: string;
  slug: string;
  summary: string;
  category: NewsCategory;
  coverImageUrl?: string | null;
  authorDisplayName: string;
  isPublished: boolean;
  isArchived: boolean;
  createdAtUtc: string;
  publishedAtUtc?: string | null;
  updatedAtUtc?: string | null;
}

export interface NewsArticleDetails extends NewsArticleSummary {
  body: string;
}

export async function listPublishedNews(
  category?: NewsCategory,
  limit = 20,
  signal?: AbortSignal,
): Promise<NewsArticleSummary[]> {
  const { data } = await httpClient.get<NewsArticleSummary[]>("/api/news", {
    params: category ? { category, limit } : { limit },
    signal,
  });
  return data;
}

export async function listAllNews(
  signal?: AbortSignal,
): Promise<NewsArticleSummary[]> {
  const { data } = await httpClient.get<NewsArticleSummary[]>(
    "/api/news/admin",
    { signal },
  );
  return data;
}

export interface CreateNewsInput {
  title: string;
  summary: string;
  body: string;
  category: NewsCategory;
  authorDisplayName: string;
  coverImageUrl?: string;
}

export async function createNewsArticle(
  input: CreateNewsInput,
): Promise<NewsArticleDetails> {
  const { data } = await httpClient.post<NewsArticleDetails>(
    "/api/news",
    input,
  );
  return data;
}

export async function publishNewsArticle(id: string): Promise<NewsArticleDetails> {
  const { data } = await httpClient.post<NewsArticleDetails>(
    `/api/news/${encodeURIComponent(id)}/publish`,
  );
  return data;
}

export async function archiveNewsArticle(id: string): Promise<void> {
  await httpClient.post(`/api/news/${encodeURIComponent(id)}/archive`);
}

export async function getNewsBySlug(
  slug: string,
  signal?: AbortSignal,
): Promise<NewsArticleDetails> {
  const { data } = await httpClient.get<NewsArticleDetails>(
    `/api/news/${encodeURIComponent(slug)}`,
    { signal },
  );
  return data;
}
