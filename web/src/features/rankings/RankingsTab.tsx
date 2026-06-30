import { useEffect, useState } from "react";
import { getAthleteRankings, type AthleteRanking } from "../../services/rankingsApi";

export function RankingsTab() {
  const [rankings, setRankings] = useState<AthleteRanking[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const ctrl = new AbortController();
    setLoading(true);
    getAthleteRankings(50, ctrl.signal)
      .then((r) => {
        setRankings(r);
        setError(null);
      })
      .catch(() => setError("Failed to load rankings."))
      .finally(() => setLoading(false));
    return () => ctrl.abort();
  }, []);

  return (
    <section className="space-y-4">
      <div>
        <h3 className="text-lg font-semibold text-slate-900">Athlete medal standings</h3>
        <p className="text-sm text-slate-500">
          Derived from completed tournaments. Gold → Silver → Bronze → wins
          is the tie-break order.
        </p>
      </div>

      {error && (
        <div className="rounded-lg border border-red-300 bg-red-50 px-4 py-3 text-sm text-red-800">
          {error}
        </div>
      )}

      <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
        <table className="min-w-full divide-y divide-slate-200 text-sm">
          <thead className="bg-slate-50 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
            <tr>
              <th className="px-4 py-3">#</th>
              <th className="px-4 py-3">Athlete</th>
              <th className="px-4 py-3">SMF ID</th>
              <th className="px-4 py-3 text-right">Gold</th>
              <th className="px-4 py-3 text-right">Silver</th>
              <th className="px-4 py-3 text-right">Bronze</th>
              <th className="px-4 py-3 text-right">W-L</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {loading && rankings.length === 0 && (
              <tr><td className="px-4 py-6 text-slate-400" colSpan={7}>Loading…</td></tr>
            )}
            {!loading && rankings.length === 0 && (
              <tr><td className="px-4 py-6 text-slate-400" colSpan={7}>
                No completed tournaments yet — generate a bracket, play some
                matches, and then come back.
              </td></tr>
            )}
            {rankings.map((r, i) => (
              <tr key={r.memberId} className="hover:bg-slate-50/60">
                <td className="px-4 py-3 text-slate-500">{i + 1}</td>
                <td className="px-4 py-3 font-medium text-slate-900">{r.fullName}</td>
                <td className="px-4 py-3 font-mono text-xs text-slate-600">{r.smF_ID}</td>
                <td className="px-4 py-3 text-right font-semibold text-amber-600">{r.gold}</td>
                <td className="px-4 py-3 text-right font-semibold text-slate-500">{r.silver}</td>
                <td className="px-4 py-3 text-right font-semibold text-orange-600">{r.bronze}</td>
                <td className="px-4 py-3 text-right text-xs text-slate-500">
                  {r.matchesWon}-{r.matchesLost}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
