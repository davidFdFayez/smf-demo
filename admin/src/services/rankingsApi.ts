import { httpClient } from "./httpClient";

export interface AthleteRanking {
  memberId: string;
  smF_ID: string;
  fullName: string;
  gold: number;
  silver: number;
  bronze: number;
  totalMedals: number;
  matchesWon: number;
  matchesLost: number;
  affiliatedClubId?: string | null;
}

export async function getAthleteRankings(
  limit = 50,
  signal?: AbortSignal,
): Promise<AthleteRanking[]> {
  const { data } = await httpClient.get<AthleteRanking[]>(
    "/api/rankings/athletes",
    { params: { limit }, signal },
  );
  return data;
}
