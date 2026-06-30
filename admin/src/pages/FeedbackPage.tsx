import { useCallback, useEffect, useState } from "react";
import {
  listFeedback,
  moderateFeedback,
  type FeedbackDto,
  type FeedbackStatus,
  type FeedbackSubjectType,
} from "../services/communicationApi";
import type { PagedResult } from "../services/storeApi";

const STATUSES: FeedbackStatus[] = ["Pending", "Public", "Hidden"];
const SUBJECT_TYPES: FeedbackSubjectType[] = ["General", "Event", "Product", "Course"];

const STATUS_STYLE: Record<FeedbackStatus, string> = {
  Pending: "bg-amber-50 text-amber-800 border-amber-200",
  Public:  "bg-emerald-50 text-emerald-800 border-emerald-200",
  Hidden:  "bg-slate-100 text-slate-600 border-slate-300",
};

export function FeedbackPage() {
  const [page, setPage] = useState(1);
  const [statusFilter, setStatusFilter] = useState<FeedbackStatus | "">("Pending");
  const [subjectType,  setSubjectType]  = useState<FeedbackSubjectType | "">("");
  const [minRating,    setMinRating]    = useState<number | "">("");
  const [data, setData] = useState<PagedResult<FeedbackDto> | null>(null);
  const [loading, setLoading] = useState(false);
  const [error,   setError]   = useState<string | null>(null);
  const [busyId,  setBusyId]  = useState<string | null>(null);

  const reload = useCallback(async () => {
    setLoading(true); setError(null);
    try {
      const res = await listFeedback({
        page, pageSize: 25,
        status:      statusFilter || undefined,
        subjectType: subjectType || undefined,
        minRating:   minRating === "" ? undefined : Number(minRating),
      });
      setData(res);
    } catch (err) { setError((err as Error).message); }
    finally { setLoading(false); }
  }, [page, statusFilter, subjectType, minRating]);

  useEffect(() => { reload(); }, [reload]);

  async function moderate(id: string, status: FeedbackStatus) {
    setBusyId(id);
    try {
      await moderateFeedback(id, { newStatus: status });
      await reload();
    } catch (err) { alert((err as Error).message); }
    finally { setBusyId(null); }
  }

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-2xl font-bold text-slate-900">Feedback &amp; reviews</h1>
        <p className="mt-1 text-sm text-slate-500">
          Moderate user submissions before they appear on the public site.
        </p>
      </header>

      <section className="rounded-2xl border border-slate-200 bg-white shadow-sm">
        <div className="flex flex-wrap items-center gap-2 border-b border-slate-200 p-4">
          <select value={statusFilter}
            onChange={(e) => { setStatusFilter(e.target.value as FeedbackStatus | ""); setPage(1); }}
            className="rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-sm">
            <option value="">All statuses</option>
            {STATUSES.map((s) => <option key={s} value={s}>{s}</option>)}
          </select>
          <select value={subjectType}
            onChange={(e) => { setSubjectType(e.target.value as FeedbackSubjectType | ""); setPage(1); }}
            className="rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-sm">
            <option value="">All subjects</option>
            {SUBJECT_TYPES.map((s) => <option key={s} value={s}>{s}</option>)}
          </select>
          <select value={minRating}
            onChange={(e) => { setMinRating(e.target.value === "" ? "" : Number(e.target.value)); setPage(1); }}
            className="rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-sm">
            <option value="">Any rating</option>
            {[5, 4, 3, 2, 1].map((r) => <option key={r} value={r}>≥ {r} ★</option>)}
          </select>
          <button type="button" onClick={() => reload()}
            className="ml-auto rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-50">
            Refresh
          </button>
        </div>

        {error && <div className="border-b border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>}

        <ul className="divide-y divide-slate-200">
          {loading && !data && <li className="px-4 py-6 text-center text-slate-500">Loading…</li>}
          {data?.items.length === 0 && !loading && <li className="px-4 py-6 text-center text-slate-500">No feedback matches.</li>}
          {data?.items.map((f) => (
            <li key={f.id} className="space-y-2 px-4 py-4">
              <div className="flex flex-wrap items-center gap-2">
                <span className="text-amber-500">{"★".repeat(f.rating)}<span className="text-slate-300">{"★".repeat(5 - f.rating)}</span></span>
                <span className="text-sm font-medium text-slate-800">{f.authorName}</span>
                {f.authorEmail && <span className="text-xs text-slate-500">{f.authorEmail}</span>}
                <span className="text-xs text-slate-500">· {f.subjectType}</span>
                <span className={`ml-auto inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium ${STATUS_STYLE[f.status]}`}>
                  {f.status}
                </span>
              </div>
              <p className="whitespace-pre-wrap text-sm text-slate-700">{f.comment}</p>
              <div className="flex flex-wrap items-center justify-between gap-2 text-xs text-slate-500">
                <span>{new Date(f.createdAtUtc).toLocaleString()}</span>
                <div className="flex gap-1">
                  {f.status !== "Public" && (
                    <button type="button" onClick={() => moderate(f.id, "Public")} disabled={busyId === f.id}
                      className="rounded-lg bg-emerald-600 px-2.5 py-1 text-xs font-semibold text-white hover:bg-emerald-700 disabled:opacity-50">
                      Publish
                    </button>
                  )}
                  {f.status !== "Hidden" && (
                    <button type="button" onClick={() => moderate(f.id, "Hidden")} disabled={busyId === f.id}
                      className="rounded-lg border border-slate-200 px-2.5 py-1 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50">
                      Hide
                    </button>
                  )}
                </div>
              </div>
            </li>
          ))}
        </ul>

        {data && data.totalCount > data.pageSize && (
          <div className="flex items-center justify-between border-t border-slate-200 px-4 py-3">
            <div className="text-xs text-slate-500">
              Page {data.page} of {Math.max(1, Math.ceil(data.totalCount / data.pageSize))} · {data.totalCount} total
            </div>
            <div className="flex gap-2">
              <button type="button" disabled={page <= 1}
                onClick={() => setPage((p) => p - 1)}
                className="rounded-lg border border-slate-200 px-3 py-1 text-sm disabled:opacity-50">Prev</button>
              <button type="button" disabled={page * data.pageSize >= data.totalCount}
                onClick={() => setPage((p) => p + 1)}
                className="rounded-lg border border-slate-200 px-3 py-1 text-sm disabled:opacity-50">Next</button>
            </div>
          </div>
        )}
      </section>
    </div>
  );
}
