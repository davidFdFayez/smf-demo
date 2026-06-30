import { useCallback, useEffect, useRef, useState } from "react";
import { HubConnection, HubConnectionState } from "@microsoft/signalr";
import { issueDevToken } from "../../services/authApi";
import {
  buildScoringConnection,
  fetchMatchReplay,
  safeStop,
} from "../../services/scoringHub";
import type {
  FighterColor,
  ScoreOverridePayload,
  StrikeUpdatePayload,
  TimerStatePayload,
} from "./types";

export type ConnectionState =
  | "disconnected"
  | "connecting"
  | "connected"
  | "reconnecting"
  | "error";

export interface ScoringSessionConfig {
  refereeId: string;
  displayName: string;
  matchCode: string;
  /** Auto-join the match's broadcast group on connect. Default true. */
  joinOnConnect?: boolean;
}

export interface ScoringSession {
  state: ConnectionState;
  error: string | null;
  strikes: StrikeUpdatePayload[];
  overrides: ScoreOverridePayload[];
  timer: TimerStatePayload | null;
  submitStrike: (color: FighterColor) => Promise<void>;
  overrideScore: (red: number, blue: number, round: number | null) => Promise<void>;
  startRound: (roundNumber: number, durationSeconds: number) => Promise<void>;
  pauseRound: () => Promise<void>;
  resumeRound: () => Promise<void>;
  endRound: () => Promise<void>;
  requestTimerState: () => Promise<void>;
}

/**
 * Connects to the scoring hub as the given referee and exposes live strike /
 * override streams plus typed actions. Automatically replays history via the
 * REST replay endpoint on connect so late joiners don't start blank.
 */
export function useScoringSession(config: ScoringSessionConfig | null): ScoringSession {
  const [state, setState] = useState<ConnectionState>("disconnected");
  const [error, setError] = useState<string | null>(null);
  const [strikes, setStrikes] = useState<StrikeUpdatePayload[]>([]);
  const [overrides, setOverrides] = useState<ScoreOverridePayload[]>([]);
  const [timer, setTimer] = useState<TimerStatePayload | null>(null);

  const connRef = useRef<HubConnection | null>(null);
  const tokenRef = useRef<string | null>(null);

  useEffect(() => {
    if (!config) {
      setState("disconnected");
      return;
    }

    let cancelled = false;
    setState("connecting");
    setError(null);

    const run = async () => {
      try {
        const { accessToken } = await issueDevToken(config.refereeId, config.displayName);
        if (cancelled) return;
        tokenRef.current = accessToken;

        const connection = buildScoringConnection(() => tokenRef.current ?? "");
        connRef.current = connection;

        connection.on("ReceiveStrikeUpdate", (payload: StrikeUpdatePayload) => {
          if (payload.matchId !== config.matchCode) return;
          setStrikes((prev) => [...prev, payload]);
        });
        connection.on("ReceiveScoreOverride", (payload: ScoreOverridePayload) => {
          if (payload.matchId !== config.matchCode) return;
          setOverrides((prev) => [...prev, payload]);
        });
        connection.on("ReceiveTimerUpdate", (payload: TimerStatePayload) => {
          if (payload.matchId !== config.matchCode) return;
          setTimer((prev) => {
            if (
              prev &&
              prev.currentRound === payload.currentRound &&
              prev.roundDurationSeconds === payload.roundDurationSeconds &&
              prev.elapsedSeconds === payload.elapsedSeconds &&
              prev.isRunning === payload.isRunning &&
              prev.lastAction === payload.lastAction
            ) {
              return prev;
            }
            return payload;
          });
        });

        connection.onreconnecting(() => setState("reconnecting"));
        connection.onreconnected(() => {
          setState("connected");
          // re-join the group after a reconnect
          if (config.joinOnConnect !== false) {
            void connection.invoke("JoinMatch", config.matchCode);
            void connection.invoke("RequestTimerState", config.matchCode).catch(() => undefined);
          }
        });
        connection.onclose(() => setState("disconnected"));

        await connection.start();
        if (cancelled) {
          await safeStop(connection);
          return;
        }

        // Replay history BEFORE joining to avoid a burst of duplicate live events.
        try {
          const replay = await fetchMatchReplay(config.matchCode);
          if (cancelled) return;
          setStrikes(
            replay.strikes.map((s) => ({
              eventId: s.eventId,
              matchId: replay.matchCode,
              refereeId: s.refereeId,
              fighterColor: s.fighterColor,
              occurredAtUtc: s.occurredAtUtc,
            })),
          );
          setOverrides(
            replay.overrides.map((o) => ({
              eventId: o.eventId,
              matchId: replay.matchCode,
              headRefereeId: o.headRefereeId,
              newScore: { red: o.red, blue: o.blue, round: o.round },
              occurredAtUtc: o.occurredAtUtc,
            })),
          );
        } catch {
          // Not fatal — live stream will still arrive; just note the blank start.
        }

        if (config.joinOnConnect !== false) {
          await connection.invoke("JoinMatch", config.matchCode);
          // Pull the current clock immediately so late joiners see it.
          try {
            await connection.invoke("RequestTimerState", config.matchCode);
          } catch {
            // non-fatal — match may not have started yet.
          }
        }

        setState("connected");
      } catch (err) {
        if (cancelled) return;
        setState("error");
        setError(err instanceof Error ? err.message : "Failed to connect to scoring hub.");
      }
    };

    void run();

    return () => {
      cancelled = true;
      void safeStop(connRef.current);
      connRef.current = null;
      setStrikes([]);
      setOverrides([]);
      setTimer(null);
    };
  }, [config?.refereeId, config?.matchCode, config?.displayName, config?.joinOnConnect]);

  const submitStrike = useCallback(
    async (color: FighterColor) => {
      const c = connRef.current;
      if (!c || c.state !== HubConnectionState.Connected) {
        throw new Error("Not connected to scoring hub.");
      }
      if (!config) return;
      await c.invoke("SubmitStrike", config.matchCode, config.refereeId, color);
    },
    [config?.matchCode, config?.refereeId],
  );

  const overrideScore = useCallback(
    async (red: number, blue: number, round: number | null) => {
      const c = connRef.current;
      if (!c || c.state !== HubConnectionState.Connected) {
        throw new Error("Not connected to scoring hub.");
      }
      if (!config) return;
      await c.invoke("OverrideScore", config.matchCode, config.refereeId, {
        red,
        blue,
        round,
      });
    },
    [config?.matchCode, config?.refereeId],
  );

  const ensureConnected = useCallback(() => {
    const c = connRef.current;
    if (!c || c.state !== HubConnectionState.Connected) {
      throw new Error("Not connected to scoring hub.");
    }
    if (!config) {
      throw new Error("Scoring session is not configured.");
    }
    return { c, cfg: config };
  }, [config?.matchCode, config?.refereeId]);

  const startRound = useCallback(
    async (roundNumber: number, durationSeconds: number) => {
      const { c, cfg } = ensureConnected();
      await c.invoke("StartRound", cfg.matchCode, cfg.refereeId, roundNumber, durationSeconds);
    },
    [ensureConnected],
  );

  const pauseRound = useCallback(async () => {
    const { c, cfg } = ensureConnected();
    await c.invoke("PauseRound", cfg.matchCode, cfg.refereeId);
  }, [ensureConnected]);

  const resumeRound = useCallback(async () => {
    const { c, cfg } = ensureConnected();
    await c.invoke("ResumeRound", cfg.matchCode, cfg.refereeId);
  }, [ensureConnected]);

  const endRound = useCallback(async () => {
    const { c, cfg } = ensureConnected();
    await c.invoke("EndRound", cfg.matchCode, cfg.refereeId);
  }, [ensureConnected]);

  const requestTimerState = useCallback(async () => {
    const { c, cfg } = ensureConnected();
    await c.invoke("RequestTimerState", cfg.matchCode);
  }, [ensureConnected]);

  return {
    state,
    error,
    strikes,
    overrides,
    timer,
    submitStrike,
    overrideScore,
    startRound,
    pauseRound,
    resumeRound,
    endRound,
    requestTimerState,
  };
}
