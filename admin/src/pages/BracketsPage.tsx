import { useEffect, useMemo, useRef, useState } from "react";
import clsx from "clsx";
import type { HubConnection } from "@microsoft/signalr";
import { issueDevToken } from "../services/authApi";
import {
  buildTournamentConnection,
  safeStopTournament,
} from "../services/tournamentHub";
import {
  generateTournament,
  getTournament,
  markBracketNoShow,
  recordMatchResult,
  type BracketMatchDto,
  type TournamentDetails,
} from "../services/tournamentsApi";
import {
  listEventRegistrations,
  listEvents,
  type EventRegistrationSummary,
  type EventSummary,
} from "../services/eventsApi";

const DEFAULT_VIEWER_ID = "11111111-1111-1111-1111-111111111111";

// A registration is "seedable" into a tournament draw once they've paid and
// been confirmed (or already checked in at the venue). No-shows and
// cancellations are filtered out.
const SEEDABLE_STATUSES: ReadonlySet<EventRegistrationSummary["status"]> = new Set([
  "Confirmed",
  "CheckedIn",
]);

/**
 * Live bracket operator screen. Paste a tournament ID, hit Load, and the
 * page:
 *   1. Fetches a snapshot via /api/tournaments/:id
 *   2. Connects to /hubs/tournaments and joins that tournament's group
 *   3. Re-renders every time the backend broadcasts `ReceiveBracketUpdate`
 *      (generation, match result, no-show, etc.)
 *
 * Each match card exposes "Red wins / Blue wins / No show" actions that
 * post to /api/tournaments/:tid/matches/:mid/result or /no-show. The
 * refresh is driven by the hub broadcast rather than an optimistic local
 * update, which keeps every operator's view identical.
 */
export function BracketsPage() {
  const [tournamentId, setTournamentId] = useState("");
  const [loaded, setLoaded] = useState<string | null>(null);
  const [details, setDetails] = useState<TournamentDetails | null>(null);
  const [state, setState] = useState<"idle" | "connecting" | "connected" | "error">(
    "idle",
  );
  const [error, setError] = useState<string | null>(null);
  const [busyMatch, setBusyMatch] = useState<string | null>(null);
  const connRef = useRef<HubConnection | null>(null);
  const tokenRef = useRef<string | null>(null);

  useEffect(() => {
    if (!loaded) {
      setState("idle");
      setDetails(null);
      return;
    }
    let cancelled = false;
    setState("connecting");
    setError(null);

    const run = async () => {
      try {
        // Snapshot first
        const snap = await getTournament(loaded);
        if (cancelled) return;
        setDetails(snap);

        const { accessToken } = await issueDevToken(DEFAULT_VIEWER_ID, "Admin operator");
        if (cancelled) return;
        tokenRef.current = accessToken;

        const conn = buildTournamentConnection(() => tokenRef.current ?? "");
        connRef.current = conn;
        conn.on("ReceiveBracketUpdate", (payload: TournamentDetails) => {
          if (payload.id !== loaded) return;
          setDetails(payload);
        });
        conn.onreconnecting(() => setState("connecting"));
        conn.onreconnected(() => {
          setState("connected");
          void conn.invoke("JoinTournament", loaded);
        });
        conn.onclose(() => setState("idle"));

        await conn.start();
        if (cancelled) {
          await safeStopTournament(conn);
          return;
        }
        await conn.invoke("JoinTournament", loaded);
        setState("connected");
      } catch (err) {
        if (cancelled) return;
        setState("error");
        setError(err instanceof Error ? err.message : "Failed to connect.");
      }
    };
    void run();

    return () => {
      cancelled = true;
      void safeStopTournament(connRef.current);
      connRef.current = null;
    };
  }, [loaded]);

  const rounds = useMemo(
    () =>
      details
        ? details.matches.reduce<Record<number, BracketMatchDto[]>>((acc, m) => {
            (acc[m.round] ??= []).push(m);
            return acc;
          }, {})
        : null,
    [details],
  );
  const roundNumbers = rounds ? Object.keys(rounds).map(Number).sort((a, b) => a - b) : [];
  const totalRounds = roundNumbers.length;

  const act = (fn: () => Promise<void>, matchId: string) => async () => {
    setBusyMatch(matchId);
    setError(null);
    try {
      await fn();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusyMatch(null);
    }
  };

  return (
    <div className="space-y-5">
      <div>
        <h2 className="text-xl font-semibold text-slate-900">Brackets</h2>
        <p className="text-sm text-slate-500">
          Real-time tournament bracket engine. Paste a tournament ID to
          subscribe to live updates, or generate a fresh bracket below.
        </p>
      </div>

      <GenerateFromRegistrationsPanel
        onGenerated={(t) => {
          setTournamentId(t.id);
          setLoaded(t.id);
        }}
      />

      <div className="card flex flex-wrap items-end gap-3 p-5">
        <label className="flex-1">
          <span className="admin-label">Tournament ID</span>
          <input
            value={tournamentId}
            onChange={(e) => setTournamentId(e.target.value)}
            placeholder="00000000-0000-0000-0000-000000000000"
            className="admin-input mt-1 font-mono text-xs"
          />
        </label>
        <button
          onClick={() => setLoaded(tournamentId.trim() || null)}
          disabled={tournamentId.trim().length === 0}
          className="btn-primary"
        >
          Load & subscribe
        </button>
        {loaded && (
          <button onClick={() => setLoaded(null)} className="btn-secondary">
            Disconnect
          </button>
        )}
        <StateBadge state={state} />
      </div>

      {error && (
        <div className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-800">
          {error}
        </div>
      )}

      {details && (
        <div className="card p-5">
          <div className="flex items-center justify-between">
            <div>
              <h3 className="text-base font-semibold text-slate-900">{details.title}</h3>
              <div className="text-xs text-slate-500">
                {details.division} · {details.status} · Created{" "}
                {new Date(details.createdAtUtc).toLocaleString()}
              </div>
            </div>
            <a
              href={`/tournaments/${details.id}`}
              target="_blank"
              rel="noopener noreferrer"
              className="text-xs font-semibold text-brand-700 hover:underline"
            >
              Open public viewer ↗
            </a>
          </div>

          <div className="mt-5 overflow-x-auto">
            <div
              className="grid gap-8"
              style={{
                gridTemplateColumns: `repeat(${Math.max(totalRounds, 1)}, minmax(240px, 1fr))`,
              }}
            >
              {roundNumbers.map((r, ri) => {
                const ordered = rounds![r]
                  .slice()
                  .sort((a, b) => a.orderInRound - b.orderInRound);
                const pairs: BracketMatchDto[][] = [];
                for (let i = 0; i < ordered.length; i += 2) {
                  pairs.push(ordered.slice(i, i + 2));
                }
                const isFirst = ri === 0;
                const isLast = ri === totalRounds - 1;
                return (
                  <div key={r} className="flex min-w-[240px] flex-col">
                    <div className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-500">
                      {roundLabel(r, totalRounds)}
                    </div>
                    <div className="flex flex-1 flex-col justify-around gap-8">
                      {pairs.map((pair, pi) => (
                        <div
                          key={pi}
                          className="relative flex flex-col justify-around gap-4"
                        >
                          {pair.map((m) => (
                            <div key={m.id} className="relative">
                              {!isFirst && (
                                <span
                                  aria-hidden
                                  className="pointer-events-none absolute -left-4 top-1/2 h-px w-4 bg-slate-300"
                                />
                              )}
                              {!isLast && (
                                <span
                                  aria-hidden
                                  className="pointer-events-none absolute -right-4 top-1/2 h-px w-4 bg-slate-300"
                                />
                              )}
                              <AdminMatchCard
                                match={m}
                                busy={busyMatch === m.id}
                                onWinA={act(
                                  () => recordMatchResult(details.id, m.id, true),
                                  m.id,
                                )}
                                onWinB={act(
                                  () => recordMatchResult(details.id, m.id, false),
                                  m.id,
                                )}
                                onNoShow={act(
                                  () => markBracketNoShow(details.id, m.id, null),
                                  m.id,
                                )}
                              />
                            </div>
                          ))}
                          {!isLast && pair.length === 2 && (
                            <span
                              aria-hidden
                              className="pointer-events-none absolute -right-4 top-[25%] bottom-[25%] w-px bg-slate-300"
                            />
                          )}
                        </div>
                      ))}
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function AdminMatchCard({
  match,
  busy,
  onWinA,
  onWinB,
  onNoShow,
}: {
  match: BracketMatchDto;
  busy: boolean;
  onWinA: () => Promise<void>;
  onWinB: () => Promise<void>;
  onNoShow: () => Promise<void>;
}) {
  const a = match.participantAName ?? (match.participantAId ? "TBD" : "Bye");
  const b = match.participantBName ?? (match.participantBId ? "TBD" : "Bye");
  const winA = match.winnerId != null && match.winnerId === match.participantAId;
  const winB = match.winnerId != null && match.winnerId === match.participantBId;
  const canAct = match.status === "Scheduled" || match.status === "InProgress";

  return (
    <div className="rounded-xl border border-slate-200 bg-white shadow-sm">
      <Row name={a} winner={winA} />
      <div className="h-px bg-slate-100" />
      <Row name={b} winner={winB} />
      <div className="flex items-center justify-between border-t border-slate-100 px-3 py-1.5 text-[11px]">
        <span className="font-mono text-slate-500">#{match.orderInRound + 1}</span>
        <StatusLabel status={match.status} />
      </div>
      {canAct && (
        <div className="grid grid-cols-3 gap-0 border-t border-slate-100">
          <ActionBtn onClick={onWinA} disabled={busy} tone="red" label="Red wins" />
          <ActionBtn onClick={onNoShow} disabled={busy} tone="amber" label="No-show" />
          <ActionBtn onClick={onWinB} disabled={busy} tone="blue" label="Blue wins" />
        </div>
      )}
    </div>
  );
}

function ActionBtn({
  onClick,
  disabled,
  tone,
  label,
}: {
  onClick: () => void;
  disabled?: boolean;
  tone: "red" | "blue" | "amber";
  label: string;
}) {
  const cls: Record<string, string> = {
    red: "text-red-700 hover:bg-red-50",
    blue: "text-blue-700 hover:bg-blue-50",
    amber: "text-amber-700 hover:bg-amber-50",
  };
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      className={clsx(
        "px-2 py-2 text-[11px] font-semibold border-r last:border-r-0 border-slate-100 disabled:opacity-40",
        cls[tone],
      )}
    >
      {label}
    </button>
  );
}

function Row({ name, winner }: { name: string; winner: boolean }) {
  const faded = name === "Bye" || name === "TBD";
  return (
    <div
      className={clsx(
        "flex items-center justify-between px-3 py-2 text-sm",
        faded ? "text-slate-400" : "text-slate-800",
      )}
    >
      <span className="truncate">{name}</span>
      {winner && (
        <span className="rounded-full bg-emerald-100 px-1.5 py-0.5 text-[10px] font-semibold text-emerald-700">
          W
        </span>
      )}
    </div>
  );
}

function StatusLabel({ status }: { status: BracketMatchDto["status"] }) {
  const cls: Record<BracketMatchDto["status"], string> = {
    Scheduled: "text-slate-500",
    InProgress: "text-amber-600",
    Completed: "text-emerald-700",
    Walkover: "text-indigo-600",
    Cancelled: "text-rose-600",
  };
  return <span className={`font-medium ${cls[status]}`}>{status}</span>;
}

function StateBadge({ state }: { state: string }) {
  const map: Record<string, string> = {
    idle: "bg-slate-100 text-slate-600",
    connecting: "bg-amber-100 text-amber-800 animate-pulse",
    connected: "bg-emerald-100 text-emerald-800",
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

function roundLabel(round: number, totalRounds: number): string {
  const from0 = totalRounds - 1 - round;
  switch (from0) {
    case 0:
      return "Final";
    case 1:
      return "Semifinals";
    case 2:
      return "Quarterfinals";
    default:
      return `Round of ${2 ** (from0 + 1)}`;
  }
}

// ─── Bulk "Generate tournament" flow ────────────────────────────────────────

/**
 * Lets an operator pick an event, multi-select from its confirmed
 * registrations, fill in a title/division, and call `POST /api/tournaments`
 * in one shot. On success, the new tournament is auto-loaded into the
 * live bracket viewer above (via `onGenerated`), which also subscribes to
 * the hub so every other operator sees the bracket appear in real-time.
 */
function GenerateFromRegistrationsPanel({
  onGenerated,
}: {
  onGenerated: (t: TournamentDetails) => void;
}) {
  const [events, setEvents] = useState<EventSummary[] | null>(null);
  const [eventsError, setEventsError] = useState<string | null>(null);
  const [selectedEventId, setSelectedEventId] = useState<string>("");
  const [registrations, setRegistrations] = useState<EventRegistrationSummary[]>([]);
  const [loadingRegs, setLoadingRegs] = useState(false);
  const [regsError, setRegsError] = useState<string | null>(null);
  const [picked, setPicked] = useState<Set<string>>(new Set());
  const [title, setTitle] = useState("");
  const [division, setDivision] = useState("Open");
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitOk, setSubmitOk] = useState<string | null>(null);

  useEffect(() => {
    const ac = new AbortController();
    listEvents(ac.signal)
      .then(setEvents)
      .catch((e) => setEventsError((e as Error).message));
    return () => ac.abort();
  }, []);

  useEffect(() => {
    if (!selectedEventId) {
      setRegistrations([]);
      setPicked(new Set());
      return;
    }
    const ac = new AbortController();
    setLoadingRegs(true);
    setRegsError(null);
    listEventRegistrations(selectedEventId, ac.signal)
      .then((regs) => {
        const seedable = regs.filter((r) => SEEDABLE_STATUSES.has(r.status));
        setRegistrations(seedable);
        // Pre-select everyone by default — a common workflow is "generate
        // the bracket for everyone who's here".
        setPicked(new Set(seedable.map((r) => r.memberId)));
      })
      .catch((e) => setRegsError((e as Error).message))
      .finally(() => setLoadingRegs(false));
    return () => ac.abort();
  }, [selectedEventId]);

  const toggle = (memberId: string) => {
    setPicked((prev) => {
      const next = new Set(prev);
      if (next.has(memberId)) next.delete(memberId);
      else next.add(memberId);
      return next;
    });
  };

  const allSelected = picked.size === registrations.length && registrations.length > 0;
  const toggleAll = () => {
    setPicked(allSelected ? new Set() : new Set(registrations.map((r) => r.memberId)));
  };

  const canSubmit =
    !submitting &&
    picked.size >= 2 &&
    title.trim().length > 0 &&
    division.trim().length > 0 &&
    !!selectedEventId;

  const submit = async () => {
    if (!canSubmit || !selectedEventId) return;
    setSubmitting(true);
    setSubmitError(null);
    setSubmitOk(null);
    try {
      const t = await generateTournament({
        eventId: selectedEventId,
        title: title.trim(),
        division: division.trim(),
        participantIds: Array.from(picked),
      });
      setSubmitOk(`Generated "${t.title}" with ${t.matches.length} matches.`);
      onGenerated(t);
    } catch (e) {
      setSubmitError((e as Error).message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="card p-5">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h3 className="text-sm font-semibold text-slate-900">Generate bracket from registrations</h3>
          <p className="text-xs text-slate-500">
            Pick an event, seed the participants, and publish a single-
            elimination bracket. Every operator subscribed to the hub will
            see it appear instantly.
          </p>
        </div>
        {submitOk && (
          <span className="rounded-full bg-emerald-50 px-3 py-1 text-xs font-semibold text-emerald-700">
            {submitOk}
          </span>
        )}
      </div>

      {eventsError && (
        <div className="mt-3 rounded-xl border border-rose-200 bg-rose-50 p-3 text-xs text-rose-700">
          {eventsError}
        </div>
      )}

      <div className="mt-4 grid gap-3 md:grid-cols-[2fr_1fr_1fr]">
        <label className="block">
          <span className="admin-label">Event</span>
          <select
            value={selectedEventId}
            onChange={(e) => setSelectedEventId(e.target.value)}
            className="admin-input mt-1"
            disabled={!events}
          >
            <option value="">
              {events == null ? "Loading events…" : "Choose an event"}
            </option>
            {events?.map((ev) => (
              <option key={ev.id} value={ev.id}>
                {ev.title} · {new Date(ev.startsAtUtc).toLocaleDateString()}
              </option>
            ))}
          </select>
        </label>
        <label className="block">
          <span className="admin-label">Tournament title</span>
          <input
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="e.g. Men's Lightweight"
            className="admin-input mt-1"
          />
        </label>
        <label className="block">
          <span className="admin-label">Division</span>
          <input
            value={division}
            onChange={(e) => setDivision(e.target.value)}
            placeholder="Open"
            className="admin-input mt-1"
          />
        </label>
      </div>

      {selectedEventId && (
        <div className="mt-4">
          <div className="flex items-center justify-between">
            <span className="admin-label">
              Participants ({picked.size}/{registrations.length})
            </span>
            {registrations.length > 0 && (
              <button
                type="button"
                onClick={toggleAll}
                className="text-xs font-semibold text-brand-700 hover:underline"
              >
                {allSelected ? "Deselect all" : "Select all"}
              </button>
            )}
          </div>

          {loadingRegs && (
            <div className="mt-2 text-sm text-slate-500">Loading registrations…</div>
          )}
          {regsError && (
            <div className="mt-2 rounded-xl border border-rose-200 bg-rose-50 p-3 text-xs text-rose-700">
              {regsError}
            </div>
          )}
          {!loadingRegs && !regsError && registrations.length === 0 && (
            <div className="mt-2 rounded-xl border border-slate-200 bg-slate-50 p-3 text-xs text-slate-500">
              This event has no confirmed or checked-in registrations yet.
            </div>
          )}
          {registrations.length > 0 && (
            <ul className="mt-2 max-h-56 divide-y divide-slate-100 overflow-y-auto rounded-xl border border-slate-200">
              {registrations.map((r) => {
                const selected = picked.has(r.memberId);
                return (
                  <li key={r.id}>
                    <label className="flex cursor-pointer items-center gap-3 px-3 py-2 text-sm hover:bg-slate-50">
                      <input
                        type="checkbox"
                        checked={selected}
                        onChange={() => toggle(r.memberId)}
                      />
                      <span className="flex-1 truncate text-slate-800">{r.memberName}</span>
                      <span
                        className={clsx(
                          "rounded-full px-2 py-0.5 text-[10px] font-semibold",
                          r.status === "CheckedIn"
                            ? "bg-emerald-100 text-emerald-700"
                            : "bg-slate-100 text-slate-600",
                        )}
                      >
                        {r.status}
                      </span>
                    </label>
                  </li>
                );
              })}
            </ul>
          )}

          {picked.size === 1 && (
            <div className="mt-2 text-xs text-amber-700">
              Select at least 2 participants to generate a bracket.
            </div>
          )}
        </div>
      )}

      {submitError && (
        <div className="mt-3 rounded-xl border border-rose-200 bg-rose-50 p-3 text-sm text-rose-800">
          {submitError}
        </div>
      )}

      <div className="mt-4 flex items-center justify-end gap-2">
        <button
          type="button"
          onClick={submit}
          disabled={!canSubmit}
          className="btn-primary"
        >
          {submitting ? "Generating…" : `Generate bracket (${picked.size})`}
        </button>
      </div>
    </div>
  );
}
