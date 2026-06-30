import { useMemo } from "react";
import { Link, useParams } from "react-router-dom";
import { BracketView } from "../features/tournaments/BracketView";
import { useTournamentStream } from "../features/tournaments/useTournamentStream";

/**
 * Public, live-updating tournament bracket.
 *
 *   /tournaments/:id
 *
 * Uses the same dev token flow as the scoreboard to authenticate to the
 * SignalR hub — any logged-in member id works as a viewer. When the backend
 * records a match result or a walk-over, `BroadcastBracketUpdatedAsync`
 * fires and this screen re-renders with the fresh snapshot.
 */
export function BracketViewerPage() {
  const { id } = useParams<{ id: string }>();

  const config = useMemo(
    () =>
      id
        ? {
            tournamentId: id,
            // Use a deterministic dev viewer id so the hub doesn't issue a
            // fresh seat every render. A real release replaces this with the
            // authenticated user's id.
            viewerId: "33333333-3333-3333-3333-333333333333",
            viewerName: "Public viewer",
          }
        : null,
    [id],
  );

  const { state, error, tournament } = useTournamentStream(config);

  return (
    <section className="mx-auto max-w-7xl px-6 py-10">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <Link to="/events" className="text-sm text-smf-700 hover:underline">
            ← Events
          </Link>
          <h1 className="mt-1 text-3xl font-bold tracking-tight text-slate-900">
            {tournament ? tournament.title : "Tournament bracket"}
          </h1>
          {tournament && (
            <p className="text-sm text-slate-600">
              {tournament.division} · {tournament.status}
            </p>
          )}
        </div>
        <StreamBadge state={state} />
      </div>

      {error && (
        <div className="mt-6 rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800">
          {error}
        </div>
      )}

      <div className="mt-8">
        {!tournament && !error ? (
          <p className="text-sm text-slate-500">Loading bracket…</p>
        ) : tournament ? (
          <BracketView tournament={tournament} />
        ) : null}
      </div>
    </section>
  );
}

function StreamBadge({ state }: { state: string }) {
  const color: Record<string, string> = {
    connected: "bg-emerald-50 text-emerald-700 ring-emerald-500/30",
    connecting: "bg-amber-50 text-amber-700 ring-amber-500/30",
    reconnecting: "bg-amber-50 text-amber-700 ring-amber-500/30",
    error: "bg-rose-50 text-rose-700 ring-rose-500/30",
    disconnected: "bg-slate-100 text-slate-600 ring-slate-300/60",
  };
  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-medium ring-1 ring-inset ${
        color[state] ?? color.disconnected
      }`}
    >
      <span className="h-1.5 w-1.5 rounded-full bg-current" />
      Live · {state}
    </span>
  );
}
