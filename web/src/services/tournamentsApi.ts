import { httpClient } from "./httpClient";

/**
 * Mirrors SMF.Application.Features.Tournaments.TournamentDetails and the
 * SMF.Domain.Enums.BracketMatchStatus / TournamentStatus on the wire.
 * JsonStringEnumConverter is used so enums come back as identifiers.
 */

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
