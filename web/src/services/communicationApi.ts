import { httpClient } from "./httpClient";

/**
 * Public-facing client for the Communication & Engagement module:
 * feedback submission/listing, social highlights feed, push device
 * registration (mobile bridge), and chatbot streaming over SSE.
 */

export type FeedbackSubjectType = "General" | "Event" | "Product" | "Course";
export type FeedbackStatus = "Pending" | "Public" | "Hidden";
export type SocialPlatform =
  | "Instagram" | "X" | "YouTube" | "TikTok" | "Facebook" | "LinkedIn";

export interface FeedbackDto {
  id: string;
  subjectType: FeedbackSubjectType;
  subjectId?: string | null;
  rating: number;
  comment: string;
  authorMemberId?: string | null;
  authorName: string;
  authorEmail?: string | null;
  status: FeedbackStatus;
  adminNotes?: string | null;
  createdAtUtc: string;
  moderatedAtUtc?: string | null;
}

export interface SubmitFeedbackInput {
  subjectType: FeedbackSubjectType;
  subjectId?: string | null;
  rating: number;
  comment: string;
  authorMemberId?: string | null;
  authorName: string;
  authorEmail?: string | null;
}

export interface SocialHighlightDto {
  id: string;
  platform: SocialPlatform;
  caption: string;
  externalUrl: string;
  embedHtml?: string | null;
  mediaUrl?: string | null;
  displayOrder: number;
  isPublished: boolean;
  postedAtUtc?: string | null;
  createdAtUtc: string;
}

export interface FeedbackStats {
  count: number;
  averageRating: number;
}

interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export async function submitFeedback(input: SubmitFeedbackInput): Promise<FeedbackDto> {
  const { data } = await httpClient.post<FeedbackDto>("/api/feedback", input);
  return data;
}

export async function listPublicFeedback(args: {
  subjectType?: FeedbackSubjectType;
  subjectId?: string;
  minRating?: number;
  page?: number;
  pageSize?: number;
} = {}): Promise<PagedResult<FeedbackDto>> {
  const { data } = await httpClient.get<PagedResult<FeedbackDto>>(
    "/api/feedback",
    { params: clean(args) },
  );
  return data;
}

export async function feedbackStats(args: {
  subjectType?: FeedbackSubjectType;
  subjectId?: string;
} = {}): Promise<FeedbackStats> {
  const { data } = await httpClient.get<FeedbackStats>(
    "/api/feedback/stats",
    { params: clean(args) },
  );
  return data;
}

export async function listSocialHighlights(args: {
  platform?: SocialPlatform;
  take?: number;
} = {}): Promise<SocialHighlightDto[]> {
  const { data } = await httpClient.get<SocialHighlightDto[]>(
    "/api/social/highlights",
    { params: clean(args) },
  );
  return data;
}

export async function chatbotStatus(): Promise<{ configured: boolean }> {
  const { data } = await httpClient.get<{ configured: boolean }>("/api/chatbot/status");
  return data;
}

/**
 * Stream a chatbot reply over SSE. Yields the assistant's message a chunk
 * at a time so the UI can render the answer progressively. Cancels cleanly
 * when the caller passes an AbortSignal that fires.
 */
export async function* streamChatReply(
  payload: {
    memberId?: string | null;
    guestKey?: string | null;
    title?: string | null;
    messages: { role: "user" | "assistant" | "system"; content: string }[];
  },
  signal?: AbortSignal,
): AsyncGenerator<string> {
  const baseUrl = (httpClient.defaults.baseURL ?? "/").replace(/\/+$/, "");
  const response = await fetch(`${baseUrl}/api/chatbot/stream`, {
    method: "POST",
    headers: { "Content-Type": "application/json", Accept: "text/event-stream" },
    body: JSON.stringify(payload),
    signal,
  });

  if (!response.ok || !response.body) {
    const text = await response.text().catch(() => "");
    throw new Error(`Chatbot HTTP ${response.status}: ${text || "no body"}`);
  }

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = "";
  while (true) {
    const { value, done } = await reader.read();
    if (done) break;
    buffer += decoder.decode(value, { stream: true });

    // SSE frames are separated by blank lines. Split on \n\n and keep the
    // last partial frame in the buffer for the next read.
    const parts = buffer.split("\n\n");
    buffer = parts.pop() ?? "";

    for (const part of parts) {
      const lines = part.split("\n");
      for (const line of lines) {
        if (!line.startsWith("data:")) continue;
        const data = line.slice(5).trim();
        if (!data) continue;
        if (data === "[DONE]") return;
        // Backend escapes embedded newlines as \n literals.
        yield data.replace(/\\n/g, "\n");
      }
    }
  }
}

function clean<T extends object>(input: T): Partial<T> {
  const out: Partial<T> = {};
  for (const [k, v] of Object.entries(input as Record<string, unknown>)) {
    if (v !== undefined && v !== null && v !== "") {
      (out as Record<string, unknown>)[k] = v;
    }
  }
  return out;
}
