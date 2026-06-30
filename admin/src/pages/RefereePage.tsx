import { useMemo, useState } from "react";
import clsx from "clsx";
import { useScoringSession, type ConnectionState } from "../hooks/useScoringSession";
import type { FighterColor } from "../types/scoring";

const SEEDED_REFEREES = [
  { id: "11111111-1111-1111-1111-111111111111", label: "Head Referee", isHead: true },
  { id: "22222222-2222-2222-2222-222222222222", label: "Side Referee A", isHead: false },
  { id: "33333333-3333-3333-3333-333333333333", label: "Side Referee B", isHead: false },
  { id: "44444444-4444-4444-4444-444444444444", label: "Side Referee C", isHead: false },
];
const DEFAULT_MATCH = "match-001";

export function RefereePage() {
  const [refereeId, setRefereeId] = useState(SEEDED_REFEREES[1].id);
  const [matchCode, setMatchCode] = useState(DEFAULT_MATCH);
  const [activeConfig, setActiveConfig] = useState<{ refereeId: string; displayName: string; matchCode: string } | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [overrideRed, setOverrideRed] = useState(10);
  const [overrideBlue, setOverrideBlue] = useState(9);
  const [overrideRound, setOverrideRound] = useState<number | "">(3);

  const referee = useMemo(
    () => SEEDED_REFEREES.find((r) => r.id === refereeId) ?? SEEDED_REFEREES[1],
    [refereeId],
  );

  const session = useScoringSession(activeConfig);
  const connected = session.state === "connected";

  const connect = () => { setActionError(null); setActiveConfig({ refereeId, displayName: referee.label, matchCode }); };
  const disconnect = () => { setActionError(null); setActiveConfig(null); };

  const submit = async (c: FighterColor) => {
    setActionError(null);
    try { await session.submitStrike(c); } catch (e) { setActionError((e as Error).message); }
  };

  const override = async () => {
    setActionError(null);
    try {
      await session.overrideScore(overrideRed, overrideBlue, overrideRound === "" ? null : overrideRound);
    } catch (e) { setActionError((e as Error).message); }
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-semibold text-slate-900">Referee console</h2>
        <p className="text-sm text-slate-500">Live scoring — connects to the SignalR hub using a dev-seeded referee identity.</p>
      </div>

      <div className="card p-5">
        <div className="grid gap-4 md:grid-cols-2">
          <label className="block">
            <span className="admin-label">Referee</span>
            <select value={refereeId} onChange={(e) => setRefereeId(e.target.value)} disabled={Boolean(activeConfig)} className="admin-input mt-1">
              {SEEDED_REFEREES.map((r) => (
                <option key={r.id} value={r.id}>{r.label}{r.isHead ? " (head)" : ""}</option>
              ))}
            </select>
          </label>
          <label className="block">
            <span className="admin-label">Match code</span>
            <input type="text" value={matchCode} onChange={(e) => setMatchCode(e.target.value)} disabled={Boolean(activeConfig)} className="admin-input mt-1 font-mono" />
          </label>
        </div>
        <div className="mt-4 flex items-center gap-3">
          {!activeConfig
            ? <button onClick={connect} className="btn-primary">Connect</button>
            : <button onClick={disconnect} className="btn-secondary">Disconnect</button>}
          <StateBadge state={session.state} />
        </div>
        {session.error && <div className="mt-3 rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-800">Connection: {session.error}</div>}
        {actionError && <div className="mt-3 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">{actionError}</div>}
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <button
          onClick={() => void submit("Red")}
          disabled={!connected}
          className="rounded-2xl bg-red-600 px-6 py-10 text-2xl font-bold text-white shadow-lg transition hover:bg-red-700 disabled:bg-red-600/40"
        >
          Strike · Red
        </button>
        <button
          onClick={() => void submit("Blue")}
          disabled={!connected}
          className="rounded-2xl bg-blue-600 px-6 py-10 text-2xl font-bold text-white shadow-lg transition hover:bg-blue-700 disabled:bg-blue-600/40"
        >
          Strike · Blue
        </button>
      </div>

      {referee.isHead && (
        <div className="card p-5">
          <h3 className="text-sm font-semibold text-slate-900">Head referee · score override</h3>
          <p className="text-xs text-slate-500">Rejected by the server for anyone other than the assigned head.</p>
          <div className="mt-4 grid gap-3 sm:grid-cols-4">
            <NumberField label="Red" value={overrideRed} setValue={setOverrideRed} />
            <NumberField label="Blue" value={overrideBlue} setValue={setOverrideBlue} />
            <label className="block">
              <span className="admin-label">Round (optional)</span>
              <input
                type="number" min={1}
                value={overrideRound}
                onChange={(e) => setOverrideRound(e.target.value === "" ? "" : Number(e.target.value))}
                className="admin-input mt-1"
              />
            </label>
            <button onClick={() => void override()} disabled={!connected} className="btn-primary self-end">Override</button>
          </div>
        </div>
      )}

      <div className="grid gap-4 sm:grid-cols-2">
        <FeedPanel title="Live strikes">
          {session.strikes.length === 0
            ? <li className="px-4 py-6 text-center text-sm text-slate-400">No strikes yet</li>
            : session.strikes.slice().reverse().slice(0, 12).map((s) => (
              <li key={s.eventId} className="flex items-center justify-between px-4 py-2 text-sm">
                <span className={clsx(
                  "inline-flex items-center rounded-full px-2 py-0.5 text-xs font-semibold",
                  s.fighterColor === "Red" ? "bg-red-100 text-red-700" : "bg-blue-100 text-blue-700",
                )}>
                  {s.fighterColor}
                </span>
                <span className="font-mono text-xs text-slate-500">{formatTime(s.occurredAtUtc)}</span>
              </li>
            ))}
        </FeedPanel>
        <FeedPanel title="Overrides">
          {session.overrides.length === 0
            ? <li className="px-4 py-6 text-center text-sm text-slate-400">No overrides yet</li>
            : session.overrides.slice().reverse().slice(0, 8).map((o) => (
              <li key={o.eventId} className="flex items-center justify-between px-4 py-2 text-sm">
                <span className="font-semibold text-slate-800">
                  {o.newScore.red}–{o.newScore.blue}{o.newScore.round != null ? ` · R${o.newScore.round}` : ""}
                </span>
                <span className="font-mono text-xs text-slate-500">{formatTime(o.occurredAtUtc)}</span>
              </li>
            ))}
        </FeedPanel>
      </div>
    </div>
  );
}

function NumberField({ label, value, setValue }: { label: string; value: number; setValue: (n: number) => void }) {
  return (
    <label className="block">
      <span className="admin-label">{label}</span>
      <input
        type="number" min={0} value={value}
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
  return <span className={clsx("inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold", map[state])}>{state}</span>;
}

function formatTime(iso: string): string {
  try {
    return new Date(iso).toLocaleTimeString([], { hour12: false, hour: "2-digit", minute: "2-digit", second: "2-digit" });
  } catch { return iso; }
}
