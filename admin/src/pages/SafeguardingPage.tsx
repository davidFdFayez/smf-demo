import { useEffect, useState } from "react";
import clsx from "clsx";
import {
  getReportByCode,
  listSafeguardingReports,
  triageReport,
  type SafeguardingReportDetails,
  type SafeguardingReportStatus,
  type SafeguardingReportSummary,
} from "../services/safeguardingApi";

const STATUS_FILTERS: Array<"all" | SafeguardingReportStatus> = [
  "all", "Submitted", "UnderReview", "Resolved", "Dismissed",
];

export function SafeguardingPage() {
  const [reports, setReports] = useState<SafeguardingReportSummary[]>([]);
  const [filter, setFilter] = useState<"all" | SafeguardingReportStatus>("Submitted");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [active, setActive] = useState<SafeguardingReportDetails | null>(null);
  const [notes, setNotes] = useState("");

  const load = async () => {
    setLoading(true);
    try {
      const s = filter === "all" ? undefined : filter;
      setReports(await listSafeguardingReports(s));
      setError(null);
    } catch (e) { setError((e as Error).message); }
    finally { setLoading(false); }
  };
  useEffect(() => { void load(); /* eslint-disable-next-line */ }, [filter]);

  const open = async (r: SafeguardingReportSummary) => {
    try {
      const full = await getReportByCode(r.referenceCode);
      setActive(full);
      setNotes(full.reviewerNotes ?? "");
    } catch (e) { setError((e as Error).message); }
  };

  const triage = async (next: SafeguardingReportStatus) => {
    if (!active) return;
    try {
      const updated = await triageReport(active.id, next, notes || undefined);
      setActive(updated); setNotes(updated.reviewerNotes ?? "");
      await load();
    } catch (e) { setError((e as Error).message); }
  };

  return (
    <div className="space-y-5">
      <div>
        <h2 className="text-xl font-semibold text-slate-900">Safeguarding reports</h2>
        <p className="text-sm text-slate-500">
          Confidential submissions for anti-doping, safeguarding, harassment, and integrity issues.
        </p>
      </div>

      <div className="card p-3">
        <div className="flex flex-wrap gap-2">
          {STATUS_FILTERS.map((s) => (
            <button
              key={s}
              onClick={() => setFilter(s)}
              className={clsx(
                "rounded-lg px-3 py-1.5 text-xs font-medium transition",
                filter === s ? "bg-brand-600 text-white" : "bg-slate-100 text-slate-700 hover:bg-slate-200",
              )}
            >
              {s === "all" ? "All" : labelOf(s)}
            </button>
          ))}
        </div>
      </div>

      {error && <div className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-800">{error}</div>}

      <div className="grid gap-4 lg:grid-cols-[2fr_3fr]">
        <div className="card overflow-hidden">
          <div className="border-b border-slate-200 bg-slate-50 px-5 py-3 text-xs font-semibold uppercase tracking-wide text-slate-500">
            Queue · {reports.length}
          </div>
          {loading && reports.length === 0 && <div className="px-5 py-6 text-sm text-slate-500">Loading…</div>}
          {!loading && reports.length === 0 && <div className="px-5 py-6 text-sm text-slate-500">No reports in this queue.</div>}
          <ul className="divide-y divide-slate-100">
            {reports.map((r) => (
              <li key={r.id}>
                <button
                  onClick={() => void open(r)}
                  className={clsx(
                    "block w-full px-5 py-3 text-left transition hover:bg-slate-50",
                    active?.id === r.id && "bg-brand-50/50",
                  )}
                >
                  <div className="flex items-center justify-between gap-3">
                    <span className="font-mono text-xs text-slate-600">{r.referenceCode}</span>
                    <StatusBadge status={r.status} />
                  </div>
                  <div className="mt-1 text-sm font-medium text-slate-900">{r.subject}</div>
                  <div className="mt-0.5 text-xs text-slate-500">
                    {r.category} · {r.isAnonymous ? "anonymous" : "named"} ·{" "}
                    {new Date(r.submittedAtUtc).toLocaleString()}
                  </div>
                </button>
              </li>
            ))}
          </ul>
        </div>

        <div className="card p-6">
          {!active ? (
            <div className="flex h-full flex-col items-center justify-center py-12 text-center text-sm text-slate-500">
              <div className="mb-2 text-base font-medium text-slate-700">Select a report to triage</div>
              <p>Click any row in the queue on the left to view the full narrative and take action.</p>
            </div>
          ) : (
            <div className="space-y-4">
              <div className="flex items-start justify-between">
                <div>
                  <div className="font-mono text-xs text-slate-500">{active.referenceCode}</div>
                  <h3 className="mt-1 text-lg font-semibold text-slate-900">{active.subject}</h3>
                  <div className="mt-0.5 text-xs text-slate-500">
                    {active.category} · submitted {new Date(active.submittedAtUtc).toLocaleString()}
                  </div>
                </div>
                <StatusBadge status={active.status} />
              </div>

              <div>
                <div className="admin-label">Narrative</div>
                <p className="mt-1 whitespace-pre-wrap rounded-lg border border-slate-200 bg-slate-50 p-3 text-sm text-slate-800">
                  {active.description}
                </p>
              </div>

              {(active.incidentLocation || active.incidentDate) && (
                <div className="grid gap-3 md:grid-cols-2">
                  {active.incidentLocation && <Info label="Incident location" value={active.incidentLocation} />}
                  {active.incidentDate && <Info label="Incident date" value={active.incidentDate} />}
                </div>
              )}

              {!active.isAnonymous && (active.reporterName || active.reporterEmail || active.reporterPhone) && (
                <div className="grid gap-3 md:grid-cols-3">
                  {active.reporterName && <Info label="Reporter name" value={active.reporterName} />}
                  {active.reporterEmail && <Info label="Reporter email" value={active.reporterEmail} />}
                  {active.reporterPhone && <Info label="Reporter phone" value={active.reporterPhone} />}
                </div>
              )}

              <div>
                <div className="admin-label">Reviewer notes</div>
                <textarea
                  value={notes}
                  onChange={(e) => setNotes(e.target.value)}
                  rows={3}
                  className="admin-input mt-1"
                  placeholder="Internal notes — visible only to the safeguarding officer."
                />
              </div>

              <div className="flex flex-wrap gap-2 border-t border-slate-200 pt-4">
                {active.status === "Submitted" && (
                  <button onClick={() => void triage("UnderReview")} className="btn-primary">Mark under review</button>
                )}
                {active.status !== "Resolved" && active.status !== "Dismissed" && (
                  <>
                    <button onClick={() => void triage("Resolved")} className="btn-primary">Resolve</button>
                    <button onClick={() => void triage("Dismissed")} className="btn-danger">Dismiss</button>
                  </>
                )}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

function StatusBadge({ status }: { status: SafeguardingReportStatus }) {
  const map: Record<SafeguardingReportStatus, string> = {
    Submitted:    "bg-amber-50 text-amber-800 ring-amber-400/40",
    UnderReview:  "bg-sky-50 text-sky-700 ring-sky-500/30",
    Resolved:     "bg-emerald-50 text-emerald-700 ring-emerald-500/30",
    Dismissed:    "bg-slate-100 text-slate-700 ring-slate-400/40",
  };
  return <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${map[status]}`}>{labelOf(status)}</span>;
}

function labelOf(status: SafeguardingReportStatus): string {
  return status === "UnderReview" ? "Under review" : status;
}

function Info({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <div className="admin-label">{label}</div>
      <div className="mt-0.5 text-sm text-slate-800">{value}</div>
    </div>
  );
}
