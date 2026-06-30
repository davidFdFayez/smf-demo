import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { useI18n } from "../i18n";
import { listClubs, type ClubSummary } from "../services/clubsApi";

export function ClubsPage() {
  const { t } = useI18n();
  const [clubs, setClubs] = useState<ClubSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");

  useEffect(() => {
    const ctrl = new AbortController();
    setLoading(true);
    // Public directory: only show Active clubs.
    listClubs("Active", ctrl.signal)
      .then((list) => { setClubs(list); setError(null); })
      .catch(() => setError(t("common.error_generic")))
      .finally(() => setLoading(false));
    return () => ctrl.abort();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return clubs;
    return clubs.filter((c) =>
      [c.name, c.city, c.description].some((v) => (v || "").toLowerCase().includes(q)),
    );
  }, [clubs, search]);

  return (
    <section className="mx-auto max-w-7xl px-6 py-12">
      <h1 className="text-3xl font-bold tracking-tight text-slate-900 sm:text-4xl">{t("clubs.title")}</h1>
      <p className="mt-2 max-w-3xl text-slate-600">{t("clubs.subtitle")}</p>

      <div className="mt-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <input
          type="search"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Search by club name or city…"
          className="field-input max-w-md"
        />
        <span className="text-xs text-slate-500">{filtered.length} / {clubs.length}</span>
      </div>

      {error && (
        <div className="mt-6 rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-800">
          {error}
        </div>
      )}

      {loading && clubs.length === 0 ? (
        <p className="mt-8 text-sm text-slate-500">{t("common.loading")}</p>
      ) : filtered.length === 0 ? (
        <p className="mt-8 text-sm text-slate-500">{t("clubs.empty")}</p>
      ) : (
        <ul className="mt-8 grid gap-5 md:grid-cols-2 lg:grid-cols-3">
          {filtered.map((c) => (
            <li key={c.id}>
              <Link
                to={`/clubs/${encodeURIComponent(c.slug)}`}
                className="public-card block p-5 transition hover:-translate-y-0.5 hover:shadow-md"
              >
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <h3 className="truncate text-base font-semibold text-slate-900">{c.name}</h3>
                    <div className="mt-0.5 text-xs text-slate-500">{c.city}</div>
                  </div>
                  <span className="chip bg-smf-50 text-smf-700 ring-smf-500/30">
                    {c.memberCount} {t("clubs.member_count")}
                  </span>
                </div>
                {c.description && <p className="mt-3 text-sm text-slate-600">{c.description}</p>}
                <div className="mt-4 border-t border-slate-100 pt-3 text-xs text-slate-600">
                  <div>{c.contactEmail}</div>
                  <div>{c.contactPhone}</div>
                </div>
                <div className="mt-3 text-xs font-medium text-smf-700">
                  View microsite →
                </div>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
