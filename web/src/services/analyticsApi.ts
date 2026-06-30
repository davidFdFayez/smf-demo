import { httpClient } from "./httpClient";

export interface StrikeBucket {
  minuteOffset: number;
  redStrikes: number;
  blueStrikes: number;
}

export interface FighterInsight {
  label: string;
  strikeCount: number;
  strikeShare: number;
  longestStreak: number;
  strikesPerMinute: number;
}

export interface MatchInsights {
  matchCode: string;
  status: string;
  scheduledAtUtc: string;
  totalStrikes: number;
  matchDurationMinutes: number;
  red: FighterInsight;
  blue: FighterInsight;
  timeline: StrikeBucket[];
  momentumLabel: string;
  predictionNarrative: string;
  redWinProbability: number;
}

export interface FederationPulse {
  totalMembers: number;
  approvedMembers: number;
  activeClubs: number;
  upcomingEvents: number;
  publishedCourses: number;
  activeEnrollments: number;
  matchesLast30Days: number;
  strikesLast30Days: number;
  averageStrikesPerMatch: number;
  highlights: string[];
}

export async function getMatchInsights(
  matchCode: string,
  signal?: AbortSignal,
): Promise<MatchInsights> {
  const { data } = await httpClient.get<MatchInsights>(
    `/api/analytics/match/${encodeURIComponent(matchCode)}`,
    { signal },
  );
  return data;
}

export async function getFederationPulse(
  signal?: AbortSignal,
): Promise<FederationPulse> {
  const { data } = await httpClient.get<FederationPulse>(
    "/api/analytics/pulse",
    { signal },
  );
  return data;
}
