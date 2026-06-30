import { useState } from "react";
import { downloadCsv, type ExportKind } from "../services/exportApi";

const ITEMS: { kind: ExportKind; title: string; desc: string }[] = [
  { kind: "members", title: "Members",  desc: "All registered identities with role, status, and contact fields." },
  { kind: "clubs",   title: "Clubs",    desc: "Federation-wide club directory with contact info and member counts." },
  { kind: "events",  title: "Events",   desc: "Every event (draft, published, completed) with registration totals." },
];

export function ExportsPage() {
  const [busy, setBusy] = useState<ExportKind | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState<ExportKind | null>(null);

  const run = async (kind: ExportKind) => {
    setError(null); setBusy(kind); setDone(null);
    try { await downloadCsv(kind); setDone(kind); }
    catch (e) { setError((e as Error).message); }
    finally { setBusy(null); }
  };

  return (
    <div className="space-y-5">
      <div>
        <h2 className="text-xl font-semibold text-slate-900">Exports</h2>
        <p className="text-sm text-slate-500">
          Download CSV reports for offline analysis. Files are generated on-demand against the live database.
        </p>
      </div>

      {error && <div className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-800">{error}</div>}

      <div className="grid gap-4 md:grid-cols-3">
        {ITEMS.map((it) => (
          <div key={it.kind} className="card flex flex-col p-5">
            <div className="flex items-start justify-between">
              <div>
                <div className="text-base font-semibold text-slate-900">{it.title}</div>
                <div className="mt-1 text-xs text-slate-500">CSV · UTF-8</div>
              </div>
              <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-brand-50 text-brand-700">
                <svg viewBox="0 0 24 24" fill="none" width="20" height="20">
                  <path d="M12 3v12m0 0l-4-4m4 4l4-4M4 21h16" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
              </div>
            </div>
            <p className="mt-3 text-sm text-slate-600">{it.desc}</p>
            <div className="mt-auto flex items-center justify-between pt-5">
              <button onClick={() => void run(it.kind)} disabled={busy === it.kind} className="btn-primary text-xs">
                {busy === it.kind ? "Generating…" : "Download .csv"}
              </button>
              {done === it.kind && <span className="text-xs font-medium text-emerald-700">Downloaded ✓</span>}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
