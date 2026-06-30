import { httpClient } from "./httpClient";

export type LiveStreamProvider = "YouTube" | "Twitch" | "Vimeo" | "CustomEmbed";

export interface MatchLiveStreamDto {
  matchId: string;
  matchCode: string;
  provider: LiveStreamProvider | null;
  url: string | null;
}

export async function getMatchLiveStreamByCode(
  code: string,
  signal?: AbortSignal,
): Promise<MatchLiveStreamDto | null> {
  try {
    const { data } = await httpClient.get<MatchLiveStreamDto>(
      `/api/live-streams/matches/by-code/${encodeURIComponent(code)}`,
      { signal },
    );
    return data;
  } catch {
    return null;
  }
}

/**
 * Build an embeddable URL for the given provider. We keep this in a single
 * place so every page that renders a stream agrees on the transformation.
 */
export function buildEmbedUrl(
  provider: LiveStreamProvider,
  url: string,
): string {
  try {
    const parsed = new URL(url);
    if (provider === "YouTube") {
      // Accept either watch?v= or youtu.be short form.
      const id =
        parsed.searchParams.get("v") ??
        parsed.pathname.replace(/^\//, "").split("/")[0];
      return id ? `https://www.youtube.com/embed/${id}` : url;
    }
    if (provider === "Twitch") {
      const channel = parsed.pathname.replace(/^\//, "").split("/")[0];
      const host = window.location.hostname || "localhost";
      return channel
        ? `https://player.twitch.tv/?channel=${encodeURIComponent(channel)}&parent=${host}`
        : url;
    }
    if (provider === "Vimeo") {
      const id = parsed.pathname.split("/").filter(Boolean).pop();
      return id ? `https://player.vimeo.com/video/${id}` : url;
    }
    return url;
  } catch {
    return url;
  }
}
