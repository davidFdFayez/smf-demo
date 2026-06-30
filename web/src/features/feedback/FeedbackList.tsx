import { useEffect, useState } from "react";
import {
  feedbackStats,
  listPublicFeedback,
  type FeedbackDto,
  type FeedbackSubjectType,
} from "../../services/communicationApi";

interface Props {
  subjectType?: FeedbackSubjectType;
  subjectId?: string;
  minRating?: number;
  limit?: number;
  emptyMessage?: string;
}

/**
 * Public list of moderated reviews. Only items that admins have flipped
 * to "Public" are returned by the backend, so this component is safe to
 * embed anywhere.
 */
export function FeedbackList({
  subjectType,
  subjectId,
  minRating = 4,
  limit = 6,
  emptyMessage = "Be the first to leave a review!",
}: Props) {
  const [items, setItems] = useState<FeedbackDto[]>([]);
  const [stats, setStats] = useState<{ count: number; averageRating: number } | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true); setError(null);
    Promise.all([
      listPublicFeedback({ subjectType, subjectId, minRating, pageSize: limit }),
      feedbackStats({ subjectType, subjectId }),
    ])
      .then(([list, s]) => {
        if (cancelled) return;
        setItems(list.items);
        setStats(s);
      })
      .catch((err) => { if (!cancelled) setError((err as Error).message); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [subjectType, subjectId, minRating, limit]);

  if (loading) return <div className="rounded-xl border border-slate-200 bg-white p-6 text-center text-slate-500">Loading reviews…</div>;
  if (error)   return <div className="rounded-xl border border-red-200 bg-red-50 p-6 text-sm text-red-700">{error}</div>;

  return (
    <div className="space-y-4">
      {stats && stats.count > 0 && (
        <div className="flex items-center gap-3 text-sm text-slate-600">
          <span className="text-2xl font-semibold text-slate-900">
            {stats.averageRating.toFixed(1)}
          </span>
          <span className="text-amber-400 text-lg" aria-hidden>
            {"★".repeat(Math.round(stats.averageRating))}
            <span className="text-slate-300">{"★".repeat(5 - Math.round(stats.averageRating))}</span>
          </span>
          <span>· {stats.count} review{stats.count === 1 ? "" : "s"}</span>
        </div>
      )}

      {items.length === 0 ? (
        <div className="rounded-xl border border-dashed border-slate-200 bg-white p-6 text-center text-slate-500">
          {emptyMessage}
        </div>
      ) : (
        <ul className="grid gap-3 md:grid-cols-2">
          {items.map((f) => (
            <li key={f.id} className="rounded-xl border border-slate-200 bg-white p-4 shadow-card">
              <div className="flex items-center gap-2">
                <span className="text-amber-400">{"★".repeat(f.rating)}<span className="text-slate-300">{"★".repeat(5 - f.rating)}</span></span>
                <span className="text-sm font-medium text-slate-800">{f.authorName}</span>
                <span className="ms-auto text-xs text-slate-500">{new Date(f.createdAtUtc).toLocaleDateString()}</span>
              </div>
              <p className="mt-2 whitespace-pre-wrap text-sm text-slate-700">{f.comment}</p>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
