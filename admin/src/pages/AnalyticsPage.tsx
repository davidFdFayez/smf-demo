import { useEffect, useState } from "react";
import {
  getFederationPulse,
  getMatchInsights,
  type FederationPulse,
  type MatchInsights,
} from "../services/analyticsApi";

export function AnalyticsPage() {
  const [pulse, setPulse] = useState<FederationPulse | null>(null);
  const [pulseErr, setPulseErr] = useState<string | null>(null);

  useEffect(() => {
    const ctrl = new AbortController();
    getFederationPulse(ctrl.signal)
      .then(setPulse)
      .catch((e: Error) => setPulseErr(e.message));
    return () => ctrl.abort();
  }, []);

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-semibold text-slate-900">AI analytics</h2>
        <p className="text-sm text-slate-500">
          Computationally derived insights across members, matches, events, and courses.
        </p>
      </div>

      <section>
        <h3 className="text-sm font-semibold uppercase tracking-wide text-slate-500">
          Federation pulse
        </h3>
        {pulseErr && (
          <div className="mt-2 rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-800">
            {pulseErr}
          </div>
        )}
        {!pulse ? (
          <div className="mt-3 grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
            {Array.from({ length: 5 }).map((_, i) => (
              <div key={i} className="card h-24 animate-pulse bg-slate-100" />
            ))}
          </div>
        ) : (
          <>
            <div className="mt-3 grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
              <KpiCard label="Members" value={pulse.totalMembers} sub={`${pulse.approvedMembers} approved`} />
              <KpiCard label="Active clubs" value={pulse.activeClubs} />
              <KpiCard label="Upcoming events" value={pulse.upcomingEvents} />
              <KpiCard label="Published courses" value={pulse.publishedCourses} sub={`${pulse.activeEnrollments} enrollments`} />
              <KpiCard
                label="Matches (30d)"
                value={pulse.matchesLast30Days}
                sub={`avg ${pulse.averageStrikesPerMatch.toFixed(1)} strikes`}
              />
            </div>
            {pulse.highlights.length > 0 && (
              <ul className="mt-4 space-y-1 text-sm text-slate-600">
                {pulse.highlights.map((h, i) => (
                  <li key={i} className="flex gap-2">
                    <span className="mt-1 inline-block h-1.5 w-1.5 shrink-0 rounded-full bg-brand-500" />
                    <span>{h}</span>
                  </li>
                ))}
              </ul>
            )}
          </>
        )}
      </section>

      <section className="card p-5">
        <h3 className="text-sm font-semibold text-slate-900">Match insights lookup</h3>
        <p className="mt-1 text-xs text-slate-500">
          Enter a match code to compute live strike tempo, momentum, and win probability.
        </p>
        <MatchInsightsLookup />
      </section>
    </div>
  );
}

function MatchInsightsLookup() {
  const [code, setCode] = useState("match-001");
  const [data, setData] = useState<MatchInsights | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault(); setBusy(true); setErr(null); setData(null);
    try { setData(await getMatchInsights(code)); }
    catch (e) { setErr((e as Error).message); }
    finally { setBusy(false); }
  };

  return (
    <form onSubmit={submit} className="mt-3 space-y-3">
      <div className="flex flex-wrap items-end gap-2">
        <label className="block text-sm">
          <span className="admin-label">Match code</span>
          <input className="admin-input mt-1 w-64 font-mono"
            value={code} onChange={(e) => setCode(e.target.value)} />
        </label>
        <button type="submit" disabled={busy || !code.trim()} className="btn-primary">
          {busy ? "Computing…" : "Compute"}
        </button>
      </div>

      {err && <div className="rounded-xl border border-rose-200 bg-rose-50 p-3 text-sm text-rose-800">{err}</div>}

      {data && (
        <div className="rounded-xl border border-slate-200 p-4">
          <div className="flex flex-wrap items-center gap-3">
            <span className="chip bg-slate-100 text-slate-700 ring-slate-500/20">{data.status}</span>
            <span className="text-sm text-slate-600">
              {data.totalStrikes} strikes · {data.matchDurationMinutes.toFixed(1)} min
            </span>
          </div>

          <div className="mt-3 grid gap-3 sm:grid-cols-2">
            <StatRow label="Red tempo" value={`${data.red.strikesPerMinute.toFixed(1)}/min · streak ${data.red.longestStreak}`} />
            <StatRow label="Blue tempo" value={`${data.blue.strikesPerMinute.toFixed(1)}/min · streak ${data.blue.longestStreak}`} />
          </div>

          <div className="mt-4">
            <div className="flex items-center justify-between text-xs font-medium text-slate-500">
              <span>Red win probability</span>
              <span>{Math.round(data.redWinProbability * 100)}%</span>
            </div>
            <div className="mt-1 flex h-2.5 overflow-hidden rounded-full bg-slate-100">
              <div className="h-full bg-red-500" style={{ width: `${Math.round(data.redWinProbability * 100)}%` }} />
              <div className="h-full bg-blue-500" style={{ width: `${100 - Math.round(data.redWinProbability * 100)}%` }} />
            </div>
          </div>

          <p className="mt-4 text-sm text-slate-700">{data.predictionNarrative}</p>
          <p className="mt-1 text-xs italic text-slate-500">{data.momentumLabel}</p>
        </div>
      )}
    </form>
  );
}

function KpiCard({
  label,
  value,
  sub,
}: {
  label: string;
  value: number;
  sub?: string;
}) {
  return (
    <div className="card p-4">
      <div className="text-[11px] font-semibold uppercase tracking-wide text-slate-500">{label}</div>
      <div className="mt-1 text-2xl font-semibold text-slate-900">{value}</div>
      {sub && <div className="mt-0.5 text-[11px] text-slate-500">{sub}</div>}
    </div>
  );
}

function StatRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-slate-100 bg-slate-50 p-3">
      <div className="text-[11px] font-semibold uppercase tracking-wide text-slate-500">{label}</div>
      <div className="mt-0.5 text-sm font-medium text-slate-900">{value}</div>
    </div>
  );
}
