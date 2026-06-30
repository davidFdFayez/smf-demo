import { useEffect, useMemo, useState } from "react";
import {
  listPublicGovernanceDocuments,
  type GovernanceDocumentDto,
  type GovernanceDocumentType,
} from "../services/complianceApi";

const TYPE_LABEL: Record<GovernanceDocumentType, string> = {
  AnnualReport:       "Annual reports",
  AntiDopingPolicy:   "Anti-doping",
  AthleteProtection:  "Athlete protection",
  Statutes:           "Statutes",
  CodeOfConduct:      "Code of conduct",
  SafeguardingPolicy: "Safeguarding",
  FinancialReport:    "Financial reports",
  Strategy:           "Strategy",
  BoardMinutes:       "Board minutes",
  Other:              "Other",
};

const TYPE_ORDER: GovernanceDocumentType[] = [
  "AnnualReport",
  "FinancialReport",
  "Strategy",
  "Statutes",
  "AntiDopingPolicy",
  "AthleteProtection",
  "SafeguardingPolicy",
  "CodeOfConduct",
  "BoardMinutes",
  "Other",
];

/**
 * Public-facing transparency page. Lists every document the federation has
 * published — annual reports, anti-doping policies, athlete-protection
 * statutes, etc. — grouped by type. SOPC-required transparency surface.
 */
export function GovernancePage() {
  const [docs, setDocs] = useState<GovernanceDocumentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true); setError(null);
    listPublicGovernanceDocuments()
      .then((rows) => { if (!cancelled) setDocs(rows); })
      .catch((err) => { if (!cancelled) setError((err as Error).message); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, []);

  const grouped = useMemo(() => {
    const map = new Map<GovernanceDocumentType, GovernanceDocumentDto[]>();
    for (const d of docs) {
      const list = map.get(d.documentType) ?? [];
      list.push(d);
      map.set(d.documentType, list);
    }
    return map;
  }, [docs]);

  return (
    <div className="mx-auto max-w-5xl space-y-8 px-4 py-10">
      <header className="space-y-2">
        <h1 className="text-3xl font-bold text-slate-900">Governance &amp; transparency</h1>
        <p className="text-slate-600">
          The Saudi MuayThai Federation publishes its annual reports, statutes,
          anti-doping rules, and athlete-protection policies for public review,
          aligned with SOPC transparency requirements.
        </p>
      </header>

      {error && (<div className="rounded-md border border-red-300 bg-red-50 px-3 py-2 text-sm text-red-700">{error}</div>)}
      {loading && (<div className="text-sm text-slate-500">Loading documents…</div>)}
      {!loading && docs.length === 0 && (
        <div className="rounded-lg border border-slate-200 bg-slate-50 px-4 py-8 text-center text-slate-500">
          No documents have been published yet. Please check back soon.
        </div>
      )}

      <div className="space-y-6">
        {TYPE_ORDER.filter((t) => grouped.has(t)).map((type) => (
          <section key={type} className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
            <h2 className="text-lg font-semibold text-slate-800">{TYPE_LABEL[type]}</h2>
            <ul className="mt-3 divide-y divide-slate-100">
              {(grouped.get(type) ?? []).map((d) => (
                <li key={d.id} className="flex items-center justify-between gap-3 py-3">
                  <div className="min-w-0">
                    <a
                      href={d.downloadUrl}
                      target="_blank"
                      rel="noreferrer"
                      className="block truncate text-sm font-medium text-blue-700 hover:underline"
                    >
                      {d.title}
                    </a>
                    {d.description && (<p className="mt-0.5 line-clamp-2 text-xs text-slate-500">{d.description}</p>)}
                    <p className="mt-1 text-[11px] text-slate-400">
                      {d.coveringYear ? `Covers ${d.coveringYear} · ` : ""}
                      {d.originalFileName} · {(d.fileSizeBytes / 1024).toFixed(1)} KB ·{" "}
                      <span title="Tamper-evidence hash" className="font-mono">{d.sha256.slice(0, 12)}…</span>
                    </p>
                  </div>
                  <a
                    href={d.downloadUrl}
                    target="_blank"
                    rel="noreferrer"
                    className="shrink-0 rounded-md border border-slate-300 px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50"
                  >
                    Download
                  </a>
                </li>
              ))}
            </ul>
          </section>
        ))}
      </div>
    </div>
  );
}
