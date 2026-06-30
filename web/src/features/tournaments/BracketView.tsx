import type { BracketMatchDto, TournamentDetails } from "../../services/tournamentsApi";

/**
 * A read-only, column-per-round bracket visualisation.
 * Pure presentational component — no data fetching. Pair it with
 * `useTournamentStream` to get live updates.
 *
 * Layout choice: a horizontal CSS-grid (columns-per-round) with pure-CSS
 * bracket connectors. Each match in a non-final round grows an 12px-wide
 * right "arm"; adjacent pairs of matches are joined by a thin vertical
 * "bridge" at the mid-gap. Incoming (left) arms on subsequent rounds land
 * where the bridge does, so the whole thing reads like a classic
 * elimination tree while still staying responsive.
 */
export function BracketView({ tournament }: { tournament: TournamentDetails }) {
  const rounds = groupByRound(tournament.matches);
  const roundNumbers = Object.keys(rounds)
    .map((n) => Number(n))
    .sort((a, b) => a - b);

  if (roundNumbers.length === 0) {
    return (
      <div className="rounded-xl border border-slate-200 bg-white p-6 text-center text-sm text-slate-500">
        This bracket has no matches yet.
      </div>
    );
  }

  return (
    <div className="overflow-x-auto">
      <div
        className="grid gap-8"
        style={{ gridTemplateColumns: `repeat(${roundNumbers.length}, minmax(220px, 1fr))` }}
      >
        {roundNumbers.map((r, idx) => (
          <RoundColumn
            key={r}
            round={r}
            matches={rounds[r]}
            total={roundNumbers.length}
            isFirst={idx === 0}
            isLast={idx === roundNumbers.length - 1}
          />
        ))}
      </div>
    </div>
  );
}

function RoundColumn({
  round,
  matches,
  total,
  isFirst,
  isLast,
}: {
  round: number;
  matches: BracketMatchDto[];
  total: number;
  isFirst: boolean;
  isLast: boolean;
}) {
  const label = roundLabel(round, total);
  const ordered = [...matches].sort((a, b) => a.orderInRound - b.orderInRound);
  const pairs: BracketMatchDto[][] = [];
  for (let i = 0; i < ordered.length; i += 2) {
    pairs.push(ordered.slice(i, i + 2));
  }

  return (
    <div className="flex min-w-[220px] flex-col">
      <div className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-500">
        {label}
      </div>
      <div className="flex flex-1 flex-col justify-around gap-8">
        {pairs.map((pair, pi) => (
          <div
            key={pi}
            className="relative flex flex-col justify-around gap-4"
          >
            {pair.map((m) => (
              <MatchCardWithConnectors
                key={m.id}
                match={m}
                isFirst={isFirst}
                isLast={isLast}
              />
            ))}
            {/* Vertical bridge joining this pair's outgoing arms. */}
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
}

function MatchCardWithConnectors({
  match,
  isFirst,
  isLast,
}: {
  match: BracketMatchDto;
  isFirst: boolean;
  isLast: boolean;
}) {
  return (
    <div className="relative">
      {/* Incoming arm from the previous round's bridge. */}
      {!isFirst && (
        <span
          aria-hidden
          className="pointer-events-none absolute -left-4 top-1/2 h-px w-4 bg-slate-300"
        />
      )}
      {/* Outgoing arm toward this pair's bridge. */}
      {!isLast && (
        <span
          aria-hidden
          className="pointer-events-none absolute -right-4 top-1/2 h-px w-4 bg-slate-300"
        />
      )}
      <MatchCard match={match} />
    </div>
  );
}

function MatchCard({ match }: { match: BracketMatchDto }) {
  const a = match.participantAName ?? (match.participantAId ? "TBD" : "Bye");
  const b = match.participantBName ?? (match.participantBId ? "TBD" : "Bye");
  const winA = match.winnerId != null && match.winnerId === match.participantAId;
  const winB = match.winnerId != null && match.winnerId === match.participantBId;

  return (
    <div
      className={`rounded-xl border bg-white shadow-sm transition ${
        match.status === "InProgress"
          ? "border-amber-300 ring-2 ring-amber-200"
          : "border-slate-200"
      }`}
    >
      <Row name={a} winner={winA} faded={a === "Bye"} />
      <div className="h-px bg-slate-100" />
      <Row name={b} winner={winB} faded={b === "Bye"} />
      <div className="flex items-center justify-between border-t border-slate-100 px-3 py-1.5 text-[11px]">
        <StatusBadge status={match.status} />
        <span className="text-slate-400">#{match.orderInRound + 1}</span>
      </div>
    </div>
  );
}

function Row({
  name,
  winner,
  faded,
}: {
  name: string;
  winner: boolean;
  faded: boolean;
}) {
  return (
    <div
      className={`flex items-center justify-between px-3 py-2 text-sm ${
        faded ? "text-slate-400" : "text-slate-800"
      }`}
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

function StatusBadge({ status }: { status: BracketMatchDto["status"] }) {
  const cls: Record<BracketMatchDto["status"], string> = {
    Scheduled: "text-slate-500",
    InProgress: "text-amber-600",
    Completed: "text-emerald-700",
    Walkover: "text-indigo-600",
    Cancelled: "text-rose-600",
  };
  return <span className={`font-medium ${cls[status]}`}>{status}</span>;
}

function groupByRound(matches: BracketMatchDto[]): Record<number, BracketMatchDto[]> {
  return matches.reduce<Record<number, BracketMatchDto[]>>((acc, m) => {
    (acc[m.round] ??= []).push(m);
    return acc;
  }, {});
}

function roundLabel(round: number, totalRounds: number): string {
  // Count from the final back: final → SF → QF → R16 → R32 → …
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
