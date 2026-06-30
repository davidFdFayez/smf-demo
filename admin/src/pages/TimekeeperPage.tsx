import { useEffect, useMemo, useState } from "react";
import clsx from "clsx";
import { useScoringSession, type ConnectionState } from "../hooks/useScoringSession";
import { assignTimekeeper } from "../services/matchesApi";

const DEFAULT_MATCH = "match-001";
const DEFAULT_TIMEKEEPER_ID = "55555555-5555-5555-5555-555555555555";

/**
 * Timekeeper console.
 *
 * Exposes the four hub methods on MatchScoringHub that only the assigned
 * timekeeper can invoke:  StartRound / PauseRound / ResumeRound / EndRound.
 * The server broadcasts a `ReceiveTimerUpdate` after each call, which is what
 * we render here — the UI is a mirror of the authoritative server clock, not
 * a client-side stopwatch. That way referees, scoreboards and the streaming
 * overlay always agree on the time.
 *
 * A small "Assign me to this match" helper calls
 * `POST /api/matches/{code}/timekeeper` so dev operators can claim a match
 * before opening the console — in production this is done via the event
 * scheduler and the button acts as an escape hatch.
 */
export function TimekeeperPage() {
  const [timekeeperId, setTimekeeperId] = useState(DEFAULT_TIMEKEEPER_ID);
  const [matchCode, setMatchCode] = useState(DEFAULT_MATCH);
  const [duration, setDuration] = useState(120); // seconds
  const [round, setRound] = useState(1);
  const [assignMsg, setAssignMsg] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [active, setActive] = useState<{ refereeId: string; displayName: string; matchCode: string } | null>(null);

  const session = useScoringSession(active);
  const connected = session.state === "connected";

  // Mirror the server round number — once a round starts the UI automatically
  // tracks whatever the authoritative timer says.
  useEffect(() => {
    if (session.timer && session.timer.currentRound > 0) {
      setRound(session.timer.currentRound);
    }
  }, [session.timer?.currentRound]);

  const connect = () => {
    setActionError(null);
    setActive({ refereeId: timekeeperId, displayName: "Timekeeper", matchCode });
  };
  const disconnect = () => {
    setActionError(null);
    setActive(null);
  };

  const safe = (fn: () => Promise<void>) => async () => {
    setActionError(null);
    try {
      await fn();
    } catch (e) {
      setActionError((e as Error).message);
    }
  };

  const claim = async () => {
    setAssignMsg(null);
    try {
      await assignTimekeeper(matchCode, timekeeperId);
      setAssignMsg("Assigned — you can connect now.");
    } catch (e) {
      setAssignMsg((e as Error).message);
    }
  };

  // Derived display values.
  const tState = session.timer;
  const remaining = useMemo(() => {
    if (!tState) return null;
    return Math.max(0, tState.roundDurationSeconds - tState.elapsedSeconds);
  }, [tState]);

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-semibold text-slate-900">Timekeeper console</h2>
        <p className="text-sm text-slate-500">
          Drives the authoritative round clock. All referees, the public
          scoreboard and the broadcast overlay mirror the state shown here.
        </p>
      </div>

      <div className="card p-5">
        <div className="grid gap-4 md:grid-cols-3">
          <label className="block">
            <span className="admin-label">Timekeeper ID</span>
            <input
              value={timekeeperId}
              onChange={(e) => setTimekeeperId(e.target.value)}
              disabled={Boolean(active)}
              className="admin-input mt-1 font-mono text-xs"
            />
          </label>
          <label className="block">
            <span className="admin-label">Match code</span>
            <input
              value={matchCode}
              onChange={(e) => setMatchCode(e.target.value)}
              disabled={Boolean(active)}
              className="admin-input mt-1 font-mono"
            />
          </label>
          <label className="block">
            <span className="admin-label">Round duration (seconds)</span>
            <input
              type="number"
              min={10}
              max={600}
              value={duration}
              onChange={(e) => setDuration(Math.max(10, Number(e.target.value) || 10))}
              className="admin-input mt-1"
            />
          </label>
        </div>
        <div className="mt-4 flex flex-wrap items-center gap-2">
          {!active ? (
            <button onClick={connect} className="btn-primary">
              Connect
            </button>
          ) : (
            <button onClick={disconnect} className="btn-secondary">
              Disconnect
            </button>
          )}
          <button onClick={claim} className="btn-secondary">
            Assign me to this match
          </button>
          <StateBadge state={session.state} />
          {assignMsg && <span className="text-xs text-slate-500">{assignMsg}</span>}
        </div>
        {session.error && (
          <div className="mt-3 rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-800">
            Connection: {session.error}
          </div>
        )}
        {actionError && (
          <div className="mt-3 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
            {actionError}
          </div>
        )}
      </div>

      <div className="grid items-stretch gap-5 lg:grid-cols-[1fr_1fr]">
        <ClockCard remaining={remaining} timer={tState} />
        <div className="card flex flex-col justify-between p-5">
          <div>
            <h3 className="text-sm font-semibold text-slate-900">Controls</h3>
            <p className="text-xs text-slate-500">
              Only the assigned timekeeper can drive the clock.
            </p>
          </div>
          <div className="mt-4 grid grid-cols-2 gap-3">
            <label className="block text-sm">
              <span className="admin-label">Next round</span>
              <input
                type="number"
                min={1}
                value={round}
                onChange={(e) => setRound(Math.max(1, Number(e.target.value) || 1))}
                className="admin-input mt-1"
              />
            </label>
            <button
              onClick={safe(() => session.startRound(round, duration))}
              disabled={!connected}
              className="btn-primary self-end"
            >
              Start round {round}
            </button>
            <button
              onClick={safe(() => session.pauseRound())}
              disabled={!connected || !tState?.isRunning}
              className="btn-secondary"
            >
              Pause
            </button>
            <button
              onClick={safe(() => session.resumeRound())}
              disabled={!connected || (tState?.isRunning ?? true)}
              className="btn-secondary"
            >
              Resume
            </button>
            <button
              onClick={safe(() => session.endRound())}
              disabled={!connected || !tState}
              className="col-span-2 rounded-lg bg-rose-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-rose-700 disabled:cursor-not-allowed disabled:bg-rose-600/40"
            >
              End round
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

function ClockCard({
  remaining,
  timer,
}: {
  remaining: number | null;
  timer: ReturnType<typeof useScoringSession>["timer"];
}) {
  return (
    <div className="relative overflow-hidden rounded-2xl bg-gradient-to-br from-ink-900 via-ink-950 to-black p-6 text-white shadow-lg">
      <div className="flex items-center justify-between text-xs font-semibold uppercase tracking-[0.25em] text-slate-400">
        <span>Round {timer?.currentRound ?? "—"}</span>
        <span>{timer?.isRunning ? "Running" : "Paused"}</span>
      </div>
      <div className="mt-6 text-center font-mono text-[96px] font-bold leading-none tabular-nums">
        {remaining == null ? "--:--" : formatMmSs(remaining)}
      </div>
      <div className="mt-4 text-center text-xs text-slate-400">
        {timer
          ? `Last action: ${timer.lastAction} · ${new Date(
              timer.occurredAtUtc,
            ).toLocaleTimeString()}`
          : "Waiting for first StartRound…"}
      </div>
    </div>
  );
}

function StateBadge({ state }: { state: ConnectionState }) {
  const map: Record<ConnectionState, string> = {
    disconnected: "bg-slate-100 text-slate-600",
    connecting: "bg-amber-100 text-amber-800 animate-pulse",
    connected: "bg-emerald-100 text-emerald-800",
    reconnecting: "bg-amber-100 text-amber-800 animate-pulse",
    error: "bg-rose-100 text-rose-800",
  };
  return (
    <span
      className={clsx(
        "inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold",
        map[state],
      )}
    >
      {state}
    </span>
  );
}

function formatMmSs(totalSeconds: number): string {
  const s = Math.max(0, Math.floor(totalSeconds));
  const m = Math.floor(s / 60);
  const r = s % 60;
  return `${m.toString().padStart(2, "0")}:${r.toString().padStart(2, "0")}`;
}
