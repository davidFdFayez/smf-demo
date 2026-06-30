import { useCallback, useEffect, useState } from "react";
import {
  listConsentLogs,
  listParentalConsents,
  type ConsentEventType,
  type ConsentLogDto,
  type ParentalConsentDto,
  type ParentalConsentStatus,
} from "../services/complianceApi";

const STATUSES: ParentalConsentStatus[] = ["Pending", "Approved", "Declined", "Expired", "Revoked"];

const STATUS_STYLE: Record<ParentalConsentStatus, string> = {
  Pending:  "border-amber-200 bg-amber-50 text-amber-700",
  Approved: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Declined: "border-red-200 bg-red-50 text-red-700",
  Expired:  "border-slate-200 bg-slate-50 text-slate-600",
  Revoked:  "border-purple-200 bg-purple-50 text-purple-700",
};

const EVENT_STYLE: Record<ConsentEventType, string> = {
  PolicyAccepted:     "border-blue-200 bg-blue-50 text-blue-700",
  GuardianLinkIssued: "border-amber-200 bg-amber-50 text-amber-700",
  GuardianLinkOpened: "border-slate-200 bg-slate-50 text-slate-700",
  GuardianApproved:   "border-emerald-200 bg-emerald-50 text-emerald-700",
  GuardianDeclined:   "border-red-200 bg-red-50 text-red-700",
  LinkExpired:        "border-slate-200 bg-slate-50 text-slate-500",
  ConsentRevoked:     "border-purple-200 bg-purple-50 text-purple-700",
};

/**
 * Audit console for the Compliance & Governance module. Top half lists
 * parental-consent ceremonies (pending / approved / declined etc.); bottom
 * half is the append-only event log covering every touchpoint, including
 * policy acceptances by adult members.
 */
export function ConsentLogPage() {
  const [statusFilter, setStatusFilter] = useState<ParentalConsentStatus | "">("");
  const [consents, setConsents] = useState<ParentalConsentDto[]>([]);
  const [logs, setLogs] = useState<ConsentLogDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [focusConsent, setFocusConsent] = useState<string | null>(null);

  const reload = useCallback(async () => {
    setLoading(true); setError(null);
    try {
      const [c, l] = await Promise.all([
        listParentalConsents({ status: statusFilter || undefined, page: 1, pageSize: 100 }),
        listConsentLogs({ consentId: focusConsent ?? undefined, page: 1, pageSize: 200 }),
      ]);
      setConsents(c.items); setLogs(l.items);
    } catch (err) { setError((err as Error).message); }
    finally { setLoading(false); }
  }, [statusFilter, focusConsent]);

  useEffect(() => { reload(); }, [reload]);

  return (
    <div className="space-y-8 p-6">
      <header>
        <h1 className="text-2xl font-semibold">Consent &amp; audit log</h1>
        <p className="text-sm text-slate-500">
          Parental-consent ceremonies for minor athletes plus the append-only event timeline used for SOPC audits.
        </p>
      </header>

      {error && (<div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{error}</div>)}

      <section className="space-y-3">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-semibold">Parental consent ceremonies</h2>
          <div className="flex items-center gap-2">
            <select className="rounded-md border border-slate-300 px-3 py-1.5 text-sm" value={statusFilter} onChange={(e) => setStatusFilter((e.target.value || "") as ParentalConsentStatus | "")}>
              <option value="">All statuses</option>
              {STATUSES.map((s) => (<option key={s} value={s}>{s}</option>))}
            </select>
            <button onClick={reload} className="rounded-md border border-slate-300 px-3 py-1.5 text-sm hover:bg-slate-50">Refresh</button>
            {focusConsent && (
              <button onClick={() => setFocusConsent(null)} className="rounded-md border border-slate-300 px-3 py-1.5 text-xs">
                Clear log filter
              </button>
            )}
          </div>
        </div>

        <div className="overflow-x-auto rounded-lg border border-slate-200">
          <table className="min-w-full divide-y divide-slate-200">
            <thead className="bg-slate-50">
              <tr>
                {["Athlete", "Guardian", "Relation", "Email", "Issued", "Expires", "Status", ""].map((h) => (
                  <th key={h} className="px-4 py-2 text-left text-xs font-semibold uppercase tracking-wide text-slate-600">{h}</th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 bg-white">
              {consents.map((c) => (
                <tr key={c.id} className={focusConsent === c.id ? "bg-blue-50/50" : ""}>
                  <td className="px-4 py-2 text-sm font-medium">{c.memberName}</td>
                  <td className="px-4 py-2 text-sm">{c.guardianFullName}</td>
                  <td className="px-4 py-2 text-xs text-slate-600">{c.relation}</td>
                  <td className="px-4 py-2 text-xs text-slate-600">{c.guardianEmail}</td>
                  <td className="px-4 py-2 text-xs text-slate-600">{new Date(c.createdAtUtc).toLocaleString()}</td>
                  <td className="px-4 py-2 text-xs text-slate-600">{new Date(c.tokenExpiresAtUtc).toLocaleString()}</td>
                  <td className="px-4 py-2">
                    <span className={`rounded-full border px-2 py-0.5 text-xs ${STATUS_STYLE[c.status]}`}>{c.status}</span>
                  </td>
                  <td className="px-4 py-2 text-right text-xs">
                    <button onClick={() => setFocusConsent(c.id)} className="text-blue-700 hover:underline">View timeline</button>
                  </td>
                </tr>
              ))}
              {!loading && consents.length === 0 && (
                <tr><td colSpan={8} className="px-4 py-6 text-center text-sm text-slate-500">No parental-consent records yet.</td></tr>
              )}
            </tbody>
          </table>
        </div>
      </section>

      <section className="space-y-3">
        <h2 className="text-lg font-semibold">Audit event log {focusConsent ? "(filtered)" : ""}</h2>
        <div className="overflow-x-auto rounded-lg border border-slate-200">
          <table className="min-w-full divide-y divide-slate-200">
            <thead className="bg-slate-50">
              <tr>
                {["When", "Event", "Member", "Policy", "IP", "Details"].map((h) => (
                  <th key={h} className="px-4 py-2 text-left text-xs font-semibold uppercase tracking-wide text-slate-600">{h}</th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 bg-white">
              {logs.map((l) => (
                <tr key={l.id}>
                  <td className="px-4 py-2 text-xs text-slate-600">{new Date(l.occurredAtUtc).toLocaleString()}</td>
                  <td className="px-4 py-2">
                    <span className={`rounded-full border px-2 py-0.5 text-xs ${EVENT_STYLE[l.eventType]}`}>{l.eventType}</span>
                  </td>
                  <td className="px-4 py-2 font-mono text-[11px] text-slate-500">{l.memberId?.slice(0, 8) ?? "—"}</td>
                  <td className="px-4 py-2 text-xs text-slate-600">{l.policyKind ? `${l.policyKind} · ${l.policyVersion ?? "?"}` : "—"}</td>
                  <td className="px-4 py-2 font-mono text-[11px] text-slate-500">{l.ipAddress ?? "—"}</td>
                  <td className="px-4 py-2 max-w-md truncate text-xs text-slate-500">{l.detailsJson ?? ""}</td>
                </tr>
              ))}
              {!loading && logs.length === 0 && (
                <tr><td colSpan={6} className="px-4 py-6 text-center text-sm text-slate-500">No audit events yet.</td></tr>
              )}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}
