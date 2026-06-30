import { useMemo, useState } from "react";
import clsx from "clsx";
import { useScoringSession, type ConnectionState } from "../hooks/useScoringSession";
import type { TimerStatePayload } from "../types/scoring";

const DEFAULT_HEAD_ID = "11111111-1111-1111-1111-111111111111";
const DEFAULT_MATCH = "match-001";

/**
 * Head referee dashboard.
 *
 * Combines three capabilities in one surface:
 *  1. Live feed of every side-referee strike (colour-coded).
 *  2. One-tap score override that writes a `ScoreOverrideEvent` into the
 *     append-only log — useful when a side ref mis-scores or a strike needs
 *     to be negated after video review.
 *  3. Real-time timer mirror so the head ref doesn't have to look at the
 *     timekeeper's screen to know how much time is left in the round.
 *
 * The server enforces that only the match's assigned head can call
 * `OverrideScore` — if a side ref opens this page the override panel will
 * simply reject the hub call with "Only the head referee of this match…".
 */
export function HeadRefereePage() {
  const [headId, setHeadId] = useState(DEFAULT_HEAD_ID);
  const [matchCode, setMatchCode] = useState(DEFAULT_MATCH);
  const [active, setActive] = useState<{ refereeId: string; displayName: string; matchCode: string } | null>(null);

  const session = useScoringSession(active);
  const connected = session.state === "connected";
  const [overrideRed, setOverrideRed] = useState(0);
  const [overrideBlue, setOverrideBlue] = useState(0);
  const [overrideRound, setOverrideRound] = useState<number | "">("");
  const [actionError, setActionError] = useState<string | null>(null);
  const [roundFilter, setRoundFilter] = useState<number | "all">("all");

  // Segment the strike/override streams into per-round windows derived from
  // timer broadcasts. This is how the dashboard can tell which round a
  // strike "belongs to" even though the strike payload itself doesn't
  // carry a round number.
  const windows = useMemo(
    () => buildRoundWindows(session.timerHistory),
    [session.timerHistory],
  );
  const knownRounds = useMemo(
    () =>
      Array.from(new Set([...windows.map((w) => w.round), session.timer?.currentRound ?? 1]))
        .filter((r): r is number => typeof r === "number" && r > 0)
        .sort((a, b) => a - b),
    [windows, session.timer?.currentRound],
  );

  // Filter strikes/overrides to the selected round window if one is chosen.
  const filteredStrikes = useMemo(() => {
    if (roundFilter === "all") return session.strikes;
    return session.strikes.filter((s) => strikeBelongsToRound(s.occurredAtUtc, roundFilter, windows));
  }, [session.strikes, roundFilter, windows]);

  const filteredOverrides = useMemo(() => {
    if (roundFilter === "all") return session.overrides;
    return session.overrides.filter((o) => {
      if (o.newScore.round != null) return o.newScore.round === roundFilter;
      // Overrides without an explicit round are considered "match-level" —
      // visible in every round filter.
      return true;
    });
  }, [session.overrides, roundFilter]);

  // Aggregated running score. Overrides are authoritative — they replace
  // the running score from the last override timestamp onward. When a
  // round filter is applied we compute the score over just that window.
  const score = useMemo(() => computeRunningScore(filteredStrikes, filteredOverrides), [
    filteredStrikes,
    filteredOverrides,
  ]);

  const tState = session.timer;

  const connect = () => {
    setActionError(null);
    setActive({ refereeId: headId, displayName: "Head Referee", matchCode });
  };
  const disconnect = () => {
    setActionError(null);
    setActive(null);
  };

  const override = async () => {
    setActionError(null);
    try {
      await session.overrideScore(
        overrideRed,
        overrideBlue,
        overrideRound === "" ? null : overrideRound,
      );
    } catch (e) {
      setActionError((e as Error).message);
    }
  };

  const nullifyLast = async () => {
    // Find the last strike and "erase" it by pushing an override equal to
    // the running score minus 1 point on that fighter for the current round.
    const last = session.strikes[session.strikes.length - 1];
    if (!last) return;
    const adjusted = {
      red: last.fighterColor === "Red" ? Math.max(0, score.red - 1) : score.red,
      blue: last.fighterColor === "Blue" ? Math.max(0, score.blue - 1) : score.blue,
      round: tState?.currentRound ?? null,
    };
    setActionError(null);
    try {
      await session.overrideScore(adjusted.red, adjusted.blue, adjusted.round);
    } catch (e) {
      setActionError((e as Error).message);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-semibold text-slate-900">Head referee dashboard</h2>
        <p className="text-sm text-slate-500">
          Watch every side-ref strike, override the score when required, and
          monitor the round clock — all on one screen.
        </p>
      </div>

      <div className="card p-5">
        <div className="grid gap-4 md:grid-cols-2">
          <label className="block">
            <span className="admin-label">Head referee ID</span>
            <input
              value={headId}
              onChange={(e) => setHeadId(e.target.value)}
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
        </div>
        <div className="mt-4 flex items-center gap-3">
          {!active ? (
            <button onClick={connect} className="btn-primary">
              Connect
            </button>
          ) : (
            <button onClick={disconnect} className="btn-secondary">
              Disconnect
            </button>
          )}
          <StateBadge state={session.state} />
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

      <div className="card p-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">
              Score filter
            </div>
            <div className="text-sm text-slate-600">
              {roundFilter === "all"
                ? "Showing the full match score (all rounds)."
                : `Showing score for round ${roundFilter} only.`}
            </div>
          </div>
          <div className="flex flex-wrap items-center gap-1">
            <FilterChip
              label="All rounds"
              active={roundFilter === "all"}
              onClick={() => setRoundFilter("all")}
            />
            {knownRounds.map((r) => (
              <FilterChip
                key={r}
                label={`R${r}`}
                active={roundFilter === r}
                onClick={() => setRoundFilter(r)}
              />
            ))}
          </div>
        </div>
      </div>

      <div className="grid gap-5 lg:grid-cols-[1.2fr_1fr]">
        <div className="card overflow-hidden">
          <div className="flex items-stretch bg-ink-950 text-white">
            <ScoreTile color="red" value={score.red} />
            <div className="flex flex-col items-center justify-center px-4 py-6 text-center font-mono tabular-nums">
              <div className="text-xs uppercase tracking-[0.25em] text-slate-400">
                {roundFilter === "all"
                  ? `Round ${tState?.currentRound ?? "—"}`
                  : `Round ${roundFilter}`}
              </div>
              <div className="mt-1 text-3xl font-bold">
                {tState == null
                  ? "--:--"
                  : formatMmSs(Math.max(0, tState.roundDurationSeconds - tState.elapsedSeconds))}
              </div>
              <div
                className={clsx(
                  "mt-1 text-[10px] font-semibold uppercase",
                  tState?.isRunning ? "text-emerald-400" : "text-slate-400",
                )}
              >
                {tState?.isRunning ? "Running" : "Paused"}
              </div>
            </div>
            <ScoreTile color="blue" value={score.blue} />
          </div>

          <div className="p-5">
            <h3 className="text-sm font-semibold text-slate-900">Score override</h3>
            <p className="text-xs text-slate-500">
              Server rejects the call unless you are the match's assigned
              head referee.
            </p>
            <div className="mt-3 grid gap-3 sm:grid-cols-4">
              <NumberField label="Red" value={overrideRed} setValue={setOverrideRed} />
              <NumberField label="Blue" value={overrideBlue} setValue={setOverrideBlue} />
              <label className="block">
                <span className="admin-label">Round (optional)</span>
                <input
                  type="number"
                  min={1}
                  value={overrideRound}
                  onChange={(e) =>
                    setOverrideRound(e.target.value === "" ? "" : Number(e.target.value))
                  }
                  className="admin-input mt-1"
                />
              </label>
              <button
                onClick={() => void override()}
                disabled={!connected}
                className="btn-primary self-end"
              >
                Override score
              </button>
            </div>
            <div className="mt-3 flex flex-wrap gap-2">
              <button
                onClick={() => {
                  setOverrideRed(score.red);
                  setOverrideBlue(score.blue);
                  setOverrideRound(tState?.currentRound ?? "");
                }}
                className="btn-secondary text-xs"
              >
                Load running score
              </button>
              <button
                onClick={() => void nullifyLast()}
                disabled={!connected || session.strikes.length === 0}
                className="btn-secondary text-xs"
              >
                Nullify last strike
              </button>
            </div>
          </div>
        </div>

        <FeedPanel
          title={`Live strikes (${filteredStrikes.length}${
            roundFilter === "all" ? "" : ` · R${roundFilter}`
          })`}
        >
          {filteredStrikes.length === 0 ? (
            <li className="px-4 py-6 text-center text-sm text-slate-400">
              No strikes {roundFilter === "all" ? "yet" : `in round ${roundFilter}`}.
            </li>
          ) : (
            filteredStrikes
              .slice()
              .reverse()
              .slice(0, 30)
              .map((s) => (
                <li
                  key={s.eventId}
                  className="flex items-center justify-between px-4 py-2 text-sm"
                >
                  <span
                    className={clsx(
                      "inline-flex items-center rounded-full px-2 py-0.5 text-xs font-semibold",
                      s.fighterColor === "Red"
                        ? "bg-red-100 text-red-700"
                        : "bg-blue-100 text-blue-700",
                    )}
                  >
                    {s.fighterColor}
                  </span>
                  <span className="font-mono text-[11px] text-slate-500">
                    {s.refereeId.slice(0, 8)}
                  </span>
                  <span className="font-mono text-xs text-slate-500">
                    {new Date(s.occurredAtUtc).toLocaleTimeString([], {
                      hour12: false,
                      hour: "2-digit",
                      minute: "2-digit",
                      second: "2-digit",
                    })}
                  </span>
                </li>
              ))
          )}
        </FeedPanel>
      </div>

      <FeedPanel
        title={`Overrides (${filteredOverrides.length}${
          roundFilter === "all" ? "" : ` · R${roundFilter}`
        })`}
      >
        {filteredOverrides.length === 0 ? (
          <li className="px-4 py-6 text-center text-sm text-slate-400">
            No overrides {roundFilter === "all" ? "yet" : `in round ${roundFilter}`}.
          </li>
        ) : (
          filteredOverrides
            .slice()
            .reverse()
            .slice(0, 20)
            .map((o) => (
              <li
                key={o.eventId}
                className="flex items-center justify-between px-4 py-2 text-sm"
              >
                <span className="font-semibold text-slate-800">
                  {o.newScore.red}–{o.newScore.blue}
                  {o.newScore.round != null ? ` · R${o.newScore.round}` : ""}
                </span>
                <span className="font-mono text-[11px] text-slate-500">
                  {o.headRefereeId.slice(0, 8)}
                </span>
                <span className="font-mono text-xs text-slate-500">
                  {new Date(o.occurredAtUtc).toLocaleTimeString([], {
                    hour12: false,
                    hour: "2-digit",
                    minute: "2-digit",
                    second: "2-digit",
                  })}
                </span>
              </li>
            ))
        )}
      </FeedPanel>
    </div>
  );
}

function ScoreTile({ color, value }: { color: "red" | "blue"; value: number }) {
  return (
    <div
      className={clsx(
        "flex-1 px-6 py-6 text-center",
        color === "red" ? "bg-red-600" : "bg-blue-600",
      )}
    >
      <div className="text-xs font-semibold uppercase tracking-[0.25em] text-white/80">
        {color === "red" ? "Red" : "Blue"}
      </div>
      <div className="mt-1 font-mono text-6xl font-bold leading-none tabular-nums">
        {value}
      </div>
    </div>
  );
}

function NumberField({
  label,
  value,
  setValue,
}: {
  label: string;
  value: number;
  setValue: (n: number) => void;
}) {
  return (
    <label className="block">
      <span className="admin-label">{label}</span>
      <input
        type="number"
        min={0}
        value={value}
        onChange={(e) => setValue(Math.max(0, Number(e.target.value) || 0))}
        className="admin-input mt-1"
      />
    </label>
  );
}

function FeedPanel({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="card overflow-hidden">
      <div className="border-b border-slate-200 bg-slate-50 px-4 py-2 text-xs font-semibold uppercase tracking-wide text-slate-500">
        {title}
      </div>
      <ul className="divide-y divide-slate-100">{children}</ul>
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

function FilterChip({
  label,
  active,
  onClick,
}: {
  label: string;
  active: boolean;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={clsx(
        "rounded-full px-3 py-1 text-xs font-semibold transition",
        active
          ? "bg-brand-600 text-white shadow"
          : "border border-slate-200 bg-white text-slate-600 hover:bg-slate-50",
      )}
    >
      {label}
    </button>
  );
}

// ─── Round-window derivation ────────────────────────────────────────────────

interface RoundWindow {
  round: number;
  startAt: string;
  endAt: string | null;
}

/**
 * Transform the timer history stream into a list of round windows. A new
 * window opens on every `RoundStarted`/`Reset`, and closes on the next
 * `Ended`, the next `RoundStarted` for a different round, or — for the
 * most recent round — remains open-ended.
 *
 * Pause/Resume transitions are not treated as round boundaries; a paused
 * round still belongs to that round.
 */
function buildRoundWindows(history: TimerStatePayload[]): RoundWindow[] {
  const windows: RoundWindow[] = [];
  let current: RoundWindow | null = null;

  for (const p of history) {
    const raw = p.lastAction as unknown;
    const kind = typeof raw === "number" ? numberToKind(raw) : (raw as string);

    if (kind === "RoundStarted" || kind === "Reset") {
      // Close the previous window if still open and this round is different.
      if (current && current.endAt == null && current.round !== p.currentRound) {
        current.endAt = p.occurredAtUtc;
      }
      // Open a new window only if we aren't already tracking this round.
      if (!current || current.endAt != null || current.round !== p.currentRound) {
        current = { round: p.currentRound, startAt: p.occurredAtUtc, endAt: null };
        windows.push(current);
      }
    } else if (kind === "Ended") {
      if (current && current.endAt == null) {
        current.endAt = p.occurredAtUtc;
      }
    }
    // Paused / Resumed / Tick don't affect windows.
  }
  return windows;
}

function numberToKind(n: number): string {
  switch (n) {
    case 1:
      return "RoundStarted";
    case 2:
      return "Paused";
    case 3:
      return "Resumed";
    case 4:
      return "Ended";
    case 5:
      return "Reset";
    default:
      return "Tick";
  }
}

function strikeBelongsToRound(
  occurredAtUtc: string,
  round: number,
  windows: RoundWindow[],
): boolean {
  return windows.some((w) => {
    if (w.round !== round) return false;
    if (occurredAtUtc < w.startAt) return false;
    if (w.endAt != null && occurredAtUtc > w.endAt) return false;
    return true;
  });
}

/**
 * Replays strike + override events to compute the current score.
 * Overrides are authoritative: from an override's timestamp onward the
 * score equals the override values; strikes before the next override add +1
 * to the appropriate fighter.
 */
function computeRunningScore(
  strikes: { fighterColor: "Red" | "Blue"; occurredAtUtc: string }[],
  overrides: { newScore: { red: number; blue: number }; occurredAtUtc: string }[],
): { red: number; blue: number } {
  const events = [
    ...strikes.map((s) => ({ kind: "strike" as const, at: s.occurredAtUtc, color: s.fighterColor })),
    ...overrides.map((o) => ({
      kind: "override" as const,
      at: o.occurredAtUtc,
      red: o.newScore.red,
      blue: o.newScore.blue,
    })),
  ].sort((a, b) => a.at.localeCompare(b.at));

  let red = 0;
  let blue = 0;
  for (const e of events) {
    if (e.kind === "strike") {
      if (e.color === "Red") red += 1;
      else blue += 1;
    } else {
      red = e.red;
      blue = e.blue;
    }
  }
  return { red, blue };
}
