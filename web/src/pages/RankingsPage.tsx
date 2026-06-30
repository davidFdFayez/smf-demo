import { useEffect, useState } from "react";
import { useI18n } from "../i18n";
import { getAthleteRankings, type AthleteRanking } from "../services/rankingsApi";

export function RankingsPage() {
  const { t } = useI18n();
  const [rankings, setRankings] = useState<AthleteRanking[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const ctrl = new AbortController();
    setLoading(true);
    getAthleteRankings(100, ctrl.signal)
      .then((r) => { setRankings(r); setError(null); })
      .catch(() => setError(t("common.error_generic")))
      .finally(() => setLoading(false));
    return () => ctrl.abort();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <section className="mx-auto max-w-5xl px-6 py-12">
      <h1 className="text-3xl font-bold tracking-tight text-slate-900 sm:text-4xl">{t("rankings.title")}</h1>
      <p className="mt-2 max-w-3xl text-slate-600">{t("rankings.subtitle")}</p>

      {error && (
        <div className="mt-6 rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-800">
          {error}
        </div>
      )}

      <div className="mt-8 overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-card">
        <table className="min-w-full divide-y divide-slate-200 text-sm">
          <thead className="bg-slate-50 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
            <tr>
              <th className="px-5 py-3 w-12">#</th>
              <th className="px-5 py-3">Athlete</th>
              <th className="px-5 py-3">SMF ID</th>
              <th className="px-5 py-3 text-right">🥇</th>
              <th className="px-5 py-3 text-right">🥈</th>
              <th className="px-5 py-3 text-right">🥉</th>
              <th className="px-5 py-3 text-right">W-L</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {loading && rankings.length === 0 && (
              <tr><td className="px-5 py-6 text-slate-400" colSpan={7}>{t("common.loading")}</td></tr>
            )}
            {!loading && rankings.length === 0 && (
              <tr><td className="px-5 py-8 text-center text-slate-400" colSpan={7}>{t("rankings.empty")}</td></tr>
            )}
            {rankings.map((r, i) => (
              <tr key={r.memberId} className="hover:bg-slate-50/70">
                <td className="px-5 py-3">
                  {i < 3 ? (
                    <span className={`inline-flex h-6 w-6 items-center justify-center rounded-full text-xs font-bold ${
                      i === 0 ? "bg-amber-100 text-amber-700"
                        : i === 1 ? "bg-slate-200 text-slate-700"
                        : "bg-orange-100 text-orange-700"
                    }`}>
                      {i + 1}
                    </span>
                  ) : (
                    <span className="text-slate-500">{i + 1}</span>
                  )}
                </td>
                <td className="px-5 py-3 font-medium text-slate-900">{r.fullName}</td>
                <td className="px-5 py-3 font-mono text-xs text-slate-600">{r.smF_ID}</td>
                <td className="px-5 py-3 text-right font-semibold text-amber-600 tabular-nums">{r.gold}</td>
                <td className="px-5 py-3 text-right font-semibold text-slate-500 tabular-nums">{r.silver}</td>
                <td className="px-5 py-3 text-right font-semibold text-orange-600 tabular-nums">{r.bronze}</td>
                <td className="px-5 py-3 text-right text-xs tabular-nums text-slate-500">
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
