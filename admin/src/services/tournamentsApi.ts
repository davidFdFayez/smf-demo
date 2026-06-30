import { httpClient } from "./httpClient";

export type TournamentStatus = "Draft" | "InProgress" | "Completed" | "Cancelled";

export type BracketMatchStatus =
  | "Scheduled"
  | "InProgress"
  | "Completed"
  | "Walkover"
  | "Cancelled";

export interface BracketMatchDto {
  id: string;
  round: number;
  orderInRound: number;
  participantAId: string | null;
  participantAName: string | null;
  participantBId: string | null;
  participantBName: string | null;
  status: BracketMatchStatus;
  winnerId: string | null;
}

export interface TournamentDetails {
  id: string;
  eventId: string;
  title: string;
  division: string;
  status: TournamentStatus;
  createdAtUtc: string;
  matches: BracketMatchDto[];
}

export interface GenerateTournamentInput {
  eventId: string;
  title: string;
  division: string;
  participantIds: string[];
}

export async function generateTournament(
  input: GenerateTournamentInput,
  signal?: AbortSignal,
): Promise<TournamentDetails> {
  const { data } = await httpClient.post<TournamentDetails>(
    "/api/tournaments",
    input,
    { signal },
  );
  return data;
}

export async function getTournament(
  id: string,
  signal?: AbortSignal,
): Promise<TournamentDetails> {
  const { data } = await httpClient.get<TournamentDetails>(
    `/api/tournaments/${encodeURIComponent(id)}`,
    { signal },
  );
  return data;
}

export async function listTournamentsForEvent(
  eventId: string,
  signal?: AbortSignal,
): Promise<TournamentDetails[]> {
  const { data } = await httpClient.get<TournamentDetails[]>(
    `/api/tournaments/by-event/${encodeURIComponent(eventId)}`,
    { signal },
  );
  return data;
}

export async function recordMatchResult(
  tournamentId: string,
  matchId: string,
  winnerIsA: boolean,
  signal?: AbortSignal,
): Promise<void> {
  await httpClient.post(
    `/api/tournaments/${encodeURIComponent(tournamentId)}/matches/${encodeURIComponent(matchId)}/result`,
    { winnerIsA },
    { signal },
  );
}

export async function markBracketNoShow(
  tournamentId: string,
  matchId: string,
  walkoverToA: boolean | null,
  signal?: AbortSignal,
): Promise<void> {
  await httpClient.post(
    `/api/tournaments/${encodeURIComponent(tournamentId)}/matches/${encodeURIComponent(matchId)}/no-show`,
    { walkoverToA },
    { signal },
  );
}
