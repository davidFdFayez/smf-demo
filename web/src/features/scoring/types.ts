export type FighterColor = "Red" | "Blue";

export interface StrikeUpdatePayload {
  eventId: string;
  matchId: string;
  refereeId: string;
  fighterColor: FighterColor;
  occurredAtUtc: string;
}

export interface ScoreOverridePayload {
  eventId: string;
  matchId: string;
  headRefereeId: string;
  newScore: {
    red: number;
    blue: number;
    round: number | null;
  };
  occurredAtUtc: string;
}

export interface MatchReplay {
  matchId: string;
  matchCode: string;
  strikes: ReplayStrike[];
  overrides: ReplayOverride[];
}

export interface ReplayStrike {
  eventId: string;
  refereeId: string;
  fighterColor: FighterColor;
  occurredAtUtc: string;
}

export interface ReplayOverride {
  eventId: string;
  headRefereeId: string;
  red: number;
  blue: number;
  round: number | null;
  occurredAtUtc: string;
}

/**
 * Running totals derived from the strike + override stream.
 * When an override exists, its values win from that timestamp onward (per round).
 */
export interface MatchScore {
  red: number;
  blue: number;
  round: number | null;
}

export type TimerActionKind =
  | "RoundStarted"
  | "Paused"
  | "Resumed"
  | "Ended"
  | "Reset"
  | "Tick";

export interface TimerStatePayload {
  matchId: string;
  currentRound: number;
  roundDurationSeconds: number;
  elapsedSeconds: number;
  isRunning: boolean;
  lastAction: TimerActionKind;
  occurredAtUtc: string;
}
