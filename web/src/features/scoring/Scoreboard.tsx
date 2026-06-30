import { useEffect, useMemo, useState } from "react";
import clsx from "clsx";
import { useScoringSession, type ConnectionState } from "./useScoringSession";
import type { MatchScore, TimerStatePayload } from "./types";

/**
 * A read-only observer identity. Uses a dev-seeded side-referee id for the
 * JWT; the scoreboard only ever calls JoinMatch + receives broadcasts.
 */
const OBSERVER_ID = "22222222-2222-2222-2222-222222222222";
const DEFAULT_MATCH = "match-001";

export interface ScoreboardProps {
  /** Optional pre-selected match code. When provided the input is hidden and
   * the scoreboard joins automatically. */
  matchCode?: string;
  /** Auto-join on mount when a matchCode is provided. Default true. */
  autoStart?: boolean;
  /** Hide the top controls (match picker / watch / connection badge). */
  hideControls?: boolean;
  /** Hide the raw timeline at the bottom, useful for broadcast overlays. */
  hideTimeline?: boolean;
}

export function Scoreboard({
  matchCode: incomingCode,
  autoStart = true,
  hideControls = false,
  hideTimeline = false,
}: ScoreboardProps = {}) {
  const isParentDriven = Boolean(incomingCode && autoStart && hideControls);
  const [matchCode, setMatchCode] = useState(incomingCode ?? DEFAULT_MATCH);
  const [activeMatch, setActiveMatch] = useState<string | null>(
    incomingCode && autoStart && !hideControls ? incomingCode : null,
  );

  const watchingMatch = isParentDriven ? incomingCode! : activeMatch;

  // Keep the manual picker in sync when used standalone.
  useEffect(() => {
    if (isParentDriven || !incomingCode) return;
    setMatchCode(incomingCode);
    if (autoStart) setActiveMatch(incomingCode);
  }, [incomingCode, autoStart, isParentDriven]);

  const config = useMemo(
    () =>
      watchingMatch
        ? {
            refereeId: OBSERVER_ID,
            displayName: "Public Scoreboard",
            matchCode: watchingMatch,
          }
        : null,
    [watchingMatch],
  );

  const session = useScoringSession(config);

  // Derive running score: if any override exists, use the latest (per round).
  // Otherwise tally strikes 1:1.
  const score: MatchScore = useMemo(() => {
    if (session.overrides.length > 0) {
      const latest = session.overrides[session.overrides.length - 1].newScore;
      return { red: latest.red, blue: latest.blue, round: latest.round };
    }
    const red = session.strikes.filter((s) => s.fighterColor === "Red").length;
    const blue = session.strikes.filter((s) => s.fighterColor === "Blue").length;
    return { red, blue, round: null };
  }, [session.strikes, session.overrides]);

  return (
    <section className="space-y-6">
      {!hideControls && (
        <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
          <label className="block">
            <span className="text-sm font-medium text-slate-700">Match code</span>
            <input
              type="text"
              value={matchCode}
              onChange={(e) => setMatchCode(e.target.value)}
              disabled={Boolean(activeMatch)}
              className="mt-1 block w-64 rounded-lg border-slate-300 bg-white px-3 py-2 font-mono text-sm shadow-sm focus:border-smf-500 focus:ring-smf-500 disabled:bg-slate-100"
            />
          </label>
          <div className="flex items-center gap-3">
            {!activeMatch ? (
              <button
                type="button"
                onClick={() => setActiveMatch(matchCode)}
                className="rounded-lg bg-smf-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-smf-700"
              >
                Watch match
              </button>
            ) : (
              <button
                type="button"
                onClick={() => setActiveMatch(null)}
                className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50"
              >
                Stop watching
              </button>
            )}
            <StateBadge state={session.state} />
          </div>
        </div>
      )}

      {session.timer && (
        <RoundClock timer={session.timer} />
      )}

      {session.error && (
        <div role="alert" className="rounded-lg border border-red-300 bg-red-50 px-4 py-3 text-sm text-red-800">
          {session.error}
        </div>
      )}

      <div className="grid gap-4 sm:grid-cols-2">
        <CornerCard color="Red" score={score.red} />
        <CornerCard color="Blue" score={score.blue} />
      </div>

      <div className="flex items-center justify-between rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm">
        <span className="text-slate-600">
          {score.round != null ? `Round ${score.round}` : "Match total"}
        </span>
        <span className="text-slate-500">
          {session.strikes.length} strike{session.strikes.length === 1 ? "" : "s"} ·{" "}
          {session.overrides.length} override
          {session.overrides.length === 1 ? "" : "s"}
        </span>
      </div>

      {!hideTimeline && (
      <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
        <div className="border-b border-slate-200 bg-slate-50 px-4 py-2 text-xs font-semibold uppercase tracking-wide text-slate-500">
          Timeline
        </div>
        <ul className="divide-y divide-slate-100">
          {[...session.strikes, ...session.overrides.map((o) => ({ ...o, isOverride: true as const }))]
            .sort((a, b) => (a.occurredAtUtc < b.occurredAtUtc ? 1 : -1))
            .slice(0, 20)
            .map((e) =>
              "isOverride" in e ? (
                <li
                  key={e.eventId}
                  className="flex items-center justify-between px-4 py-2 text-sm"
                >
                  <span className="inline-flex items-center gap-2">
                    <span className="rounded-full bg-amber-100 px-2 py-0.5 text-xs font-semibold text-amber-800">
                      Override
                    </span>
                    <span className="font-semibold text-slate-800">
                      {e.newScore.red}–{e.newScore.blue}
                      {e.newScore.round != null ? ` · R${e.newScore.round}` : ""}
                    </span>
                  </span>
                  <span className="font-mono text-xs text-slate-500">{formatTime(e.occurredAtUtc)}</span>
                </li>
              ) : (
                <li
                  key={e.eventId}
                  className="flex items-center justify-between px-4 py-2 text-sm"
                >
                  <span
                    className={clsx(
                      "inline-flex items-center rounded-full px-2 py-0.5 text-xs font-semibold",
                      e.fighterColor === "Red"
                        ? "bg-red-100 text-red-700"
                        : "bg-blue-100 text-blue-700",
                    )}
                  >
                    Strike · {e.fighterColor}
                  </span>
                  <span className="font-mono text-xs text-slate-500">{formatTime(e.occurredAtUtc)}</span>
                </li>
              ),
            )}
          {session.strikes.length === 0 && session.overrides.length === 0 && (
            <li className="px-4 py-8 text-center text-sm text-slate-400">
              Waiting for live scoring events…
            </li>
          )}
        </ul>
      </div>
      )}
    </section>
  );
}

function RoundClock({ timer }: { timer: TimerStatePayload }) {
  const [remaining, setRemaining] = useState(() => computeRemaining(timer));

  useEffect(() => {
    setRemaining(computeRemaining(timer));
    if (!timer.isRunning) return;
    const id = window.setInterval(() => setRemaining(computeRemaining(timer)), 1000);
    return () => window.clearInterval(id);
  }, [
    timer.currentRound,
    timer.roundDurationSeconds,
    timer.elapsedSeconds,
    timer.isRunning,
    timer.occurredAtUtc,
  ]);

  return (
    <div className="flex items-center justify-between rounded-xl border border-slate-200 bg-gradient-to-r from-slate-50 to-slate-100 px-4 py-3">
      <div className="text-xs font-semibold uppercase tracking-[0.2em] text-slate-500">
        Round {timer.currentRound}
      </div>
      <div className="flex items-baseline gap-2 font-mono text-3xl font-bold tabular-nums text-slate-900">
        {formatMmSs(remaining)}
        <span className="text-xs font-semibold uppercase text-slate-400">
          {timer.isRunning ? "running" : "paused"}
        </span>
      </div>
    </div>
  );
}

function computeRemaining(timer: TimerStatePayload): number {
  let elapsed = timer.elapsedSeconds;
  if (timer.isRunning) {
    const since = Date.now() - new Date(timer.occurredAtUtc).getTime();
    elapsed += Math.max(0, Math.floor(since / 1000));
  }
  return Math.max(0, timer.roundDurationSeconds - elapsed);
}

function formatMmSs(totalSeconds: number): string {
  const s = Math.max(0, Math.floor(totalSeconds));
  const m = Math.floor(s / 60);
  const r = s % 60;
  return `${m.toString().padStart(2, "0")}:${r.toString().padStart(2, "0")}`;
}

function CornerCard({ color, score }: { color: "Red" | "Blue"; score: number }) {
  const bg = color === "Red" ? "bg-red-600" : "bg-blue-600";
  return (
    <div
      className={clsx(
        "flex flex-col items-center justify-center rounded-2xl px-8 py-10 text-white shadow-md",
        bg,
      )}
    >
      <span className="text-xs font-semibold uppercase tracking-[0.2em] opacity-80">
        {color} corner
      </span>
      <span className="mt-2 text-7xl font-black tabular-nums tracking-tight">
        {score}
      </span>
    </div>
  );
}

function StateBadge({ state }: { state: ConnectionState }) {
  const styles: Record<ConnectionState, string> = {
    disconnected: "bg-slate-100 text-slate-600",
    connecting: "bg-amber-100 text-amber-800 animate-pulse",
    connected: "bg-emerald-100 text-emerald-800",
    reconnecting: "bg-amber-100 text-amber-800 animate-pulse",
    error: "bg-red-100 text-red-800",
  };
  return (
    <span
      className={clsx(
        "inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold",
        styles[state],
      )}
    >
      {state}
    </span>
  );
}

function formatTime(iso: string): string {
  try {
    const d = new Date(iso);
    return d.toLocaleTimeString([], {
      hour12: false,
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
    });
  } catch {
    return iso;
  }
}
