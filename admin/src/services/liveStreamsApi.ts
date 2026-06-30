import { httpClient } from "./httpClient";

export type LiveStreamProvider = "YouTube" | "Twitch" | "Vimeo" | "CustomEmbed";

export interface MatchLiveStreamDto {
  matchId: string;
  matchCode: string;
  provider: LiveStreamProvider | null;
  url: string | null;
}

export interface SetStreamInput {
  provider: LiveStreamProvider;
  url: string;
}

export async function setEventLiveStream(
  eventId: string,
  input: SetStreamInput,
): Promise<void> {
  await httpClient.put(`/api/live-streams/events/${eventId}`, input);
}

export async function clearEventLiveStream(eventId: string): Promise<void> {
  // Backend treats null provider/url as a clear operation.
  await httpClient.put(`/api/live-streams/events/${eventId}`, {
    provider: null,
    url: null,
  });
}

export async function setMatchLiveStream(
  matchId: string,
  input: SetStreamInput,
): Promise<void> {
  await httpClient.put(`/api/live-streams/matches/${matchId}`, input);
}

export async function clearMatchLiveStream(matchId: string): Promise<void> {
  await httpClient.put(`/api/live-streams/matches/${matchId}`, {
    provider: null,
    url: null,
  });
}

export async function getMatchLiveStreamByCode(
  matchCode: string,
  signal?: AbortSignal,
): Promise<MatchLiveStreamDto | null> {
  try {
    const { data } = await httpClient.get<MatchLiveStreamDto>(
      `/api/live-streams/matches/by-code/${encodeURIComponent(matchCode)}`,
      { signal },
    );
    return data;
  } catch {
    return null;
  }
}
