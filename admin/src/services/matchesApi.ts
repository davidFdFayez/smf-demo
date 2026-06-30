import { httpClient } from "./httpClient";

export interface ScheduleMatchInput {
  code: string;
  headRefereeId: string;
  scheduledAtUtc: string;
  sideRefereeIds?: string[];
}

export interface MatchSummary {
  id: string;
  code: string;
  headRefereeId: string;
  scheduledAtUtc: string;
  status: string;
}

export async function scheduleMatch(
  input: ScheduleMatchInput,
  signal?: AbortSignal,
): Promise<MatchSummary> {
  const { data } = await httpClient.post<MatchSummary>("/api/matches", input, { signal });
  return data;
}

export async function assignReferee(
  code: string,
  refereeId: string,
  signal?: AbortSignal,
): Promise<void> {
  await httpClient.post(
    `/api/matches/${encodeURIComponent(code)}/referees`,
    { refereeId },
    { signal },
  );
}

export async function assignTimekeeper(
  code: string,
  timekeeperId: string,
  signal?: AbortSignal,
): Promise<void> {
  await httpClient.post(
    `/api/matches/${encodeURIComponent(code)}/timekeeper`,
    { timekeeperId },
    { signal },
  );
}
