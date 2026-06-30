import { httpClient } from "./httpClient";

export interface MatchSummary {
  code: string;
  status: "Scheduled" | "Live" | "Completed" | "Cancelled";
  scheduledAtUtc: string;
  isTimerRunning: boolean;
  currentRound: number;
}

export async function listMatches(signal?: AbortSignal): Promise<MatchSummary[]> {
  const { data } = await httpClient.get<MatchSummary[]>("/api/matches", { signal });
  return data;
}
