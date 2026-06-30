import { useCallback, useEffect, useRef, useState } from "react";
import { HubConnection, HubConnectionState } from "@microsoft/signalr";
import { issueDevToken } from "../services/authApi";
import {
  buildScoringConnection,
  fetchMatchReplay,
  safeStop,
} from "../services/scoringHub";
import type {
  FighterColor,
  ScoreOverridePayload,
  StrikeUpdatePayload,
  TimerStatePayload,
} from "../types/scoring";

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
  joinOnConnect?: boolean;
}

export interface ScoringSession {
  state: ConnectionState;
  error: string | null;
  strikes: StrikeUpdatePayload[];
  overrides: ScoreOverridePayload[];
  timer: TimerStatePayload | null;
  /**
   * Complete log of every timer payload that came in, in arrival order.
   * The head-referee dashboard uses this to derive round windows so strikes
   * can be filtered to just one round even though the strike payload itself
   * doesn't carry a round number.
   */
  timerHistory: TimerStatePayload[];
  submitStrike: (color: FighterColor) => Promise<void>;
  overrideScore: (red: number, blue: number, round: number | null) => Promise<void>;
  startRound: (roundNumber: number, durationSeconds: number) => Promise<void>;
  pauseRound: () => Promise<void>;
  resumeRound: () => Promise<void>;
  endRound: () => Promise<void>;
  requestTimerState: () => Promise<void>;
}

export function useScoringSession(config: ScoringSessionConfig | null): ScoringSession {
  const [state, setState] = useState<ConnectionState>("disconnected");
  const [error, setError] = useState<string | null>(null);
  const [strikes, setStrikes] = useState<StrikeUpdatePayload[]>([]);
  const [overrides, setOverrides] = useState<ScoreOverridePayload[]>([]);
  const [timer, setTimer] = useState<TimerStatePayload | null>(null);
  const [timerHistory, setTimerHistory] = useState<TimerStatePayload[]>([]);

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
          setTimer(payload);
          // Keep the full history so we can compute per-round windows for
          // strike filtering. Drop "tick" events so the history only has
          // meaningful transitions (start/pause/resume/end/reset).
          // MessagePack emits the enum as its integer value (6 for Tick);
          // JSON protocol emits the string — accept both.
          const raw = payload.lastAction as unknown;
          const isTick = raw === "Tick" || raw === 6;
          if (!isTick) {
            setTimerHistory((prev) => [...prev, payload]);
          }
        });
        connection.onreconnecting(() => setState("reconnecting"));
        connection.onreconnected(() => {
          setState("connected");
          if (config.joinOnConnect !== false) {
            void connection.invoke("JoinMatch", config.matchCode);
            void connection.invoke("RequestTimerState", config.matchCode).catch(() => undefined);
          }
        });
        connection.onclose(() => setState("disconnected"));

        await connection.start();
        if (cancelled) { await safeStop(connection); return; }

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
          /* non-fatal */
        }

        if (config.joinOnConnect !== false) {
          await connection.invoke("JoinMatch", config.matchCode);
          try {
            await connection.invoke("RequestTimerState", config.matchCode);
          } catch {
            // non-fatal
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
      setTimerHistory([]);
    };
  }, [config?.refereeId, config?.matchCode, config?.displayName, config?.joinOnConnect]);

  const submitStrike = useCallback(
    async (color: FighterColor) => {
      const c = connRef.current;
      if (!c || c.state !== HubConnectionState.Connected)
        throw new Error("Not connected to scoring hub.");
      if (!config) return;
      await c.invoke("SubmitStrike", config.matchCode, config.refereeId, color);
    },
    [config?.matchCode, config?.refereeId],
  );

  const overrideScore = useCallback(
    async (red: number, blue: number, round: number | null) => {
      const c = connRef.current;
      if (!c || c.state !== HubConnectionState.Connected)
        throw new Error("Not connected to scoring hub.");
      if (!config) return;
      await c.invoke("OverrideScore", config.matchCode, config.refereeId, { red, blue, round });
    },
    [config?.matchCode, config?.refereeId],
  );

  const ensureConnected = useCallback(() => {
    const c = connRef.current;
    if (!c || c.state !== HubConnectionState.Connected) {
      throw new Error("Not connected to scoring hub.");
    }
    if (!config) throw new Error("Scoring session is not configured.");
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
    timerHistory,
    submitStrike,
    overrideScore,
    startRound,
    pauseRound,
    resumeRound,
    endRound,
    requestTimerState,
  };
}
