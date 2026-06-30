import { useCallback, useEffect, useState } from "react";
import {
  POLICY_KINDS,
  listPolicyVersions,
  publishPolicyVersion,
  type PolicyDocumentDto,
  type PolicyDocumentKind,
} from "../services/complianceApi";

/**
 * Manages the active version of each policy (Terms of Service, Privacy
 * Policy, Code of Conduct). Publishing a new version retires the previous
 * active row of the same kind so the registration form always serves a
 * single deterministic copy.
 */
export function PolicyVersionsPage() {
  const [versions, setVersions] = useState<PolicyDocumentDto[]>([]);
  const [filter, setFilter] = useState<PolicyDocumentKind | "">("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [composerOpen, setComposerOpen] = useState(false);
  const [reading, setReading] = useState<PolicyDocumentDto | null>(null);

  const reload = useCallback(async () => {
    setLoading(true); setError(null);
    try { setVersions(await listPolicyVersions(filter || undefined)); }
    catch (err) { setError((err as Error).message); }
    finally { setLoading(false); }
  }, [filter]);

  useEffect(() => { reload(); }, [reload]);

  return (
    <div className="space-y-6 p-6">
      <header className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Policy versions</h1>
          <p className="text-sm text-slate-500">
            Publish new Terms / Privacy / Code-of-Conduct versions. The audit log binds each acceptance to an immutable version row.
          </p>
        </div>
        <button onClick={() => setComposerOpen(true)} className="rounded-lg bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800">
          Publish new version
        </button>
      </header>

      <div className="flex flex-wrap items-center gap-3">
        <select className="rounded-md border border-slate-300 px-3 py-1.5 text-sm" value={filter} onChange={(e) => setFilter((e.target.value || "") as PolicyDocumentKind | "")}>
          <option value="">All kinds</option>
          {POLICY_KINDS.map((k) => (<option key={k} value={k}>{k}</option>))}
        </select>
        <button onClick={reload} className="rounded-md border border-slate-300 px-3 py-1.5 text-sm hover:bg-slate-50">Refresh</button>
      </div>

      {error && (<div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{error}</div>)}
      {loading && (<div className="text-sm text-slate-500">Loading…</div>)}

      <div className="overflow-x-auto rounded-lg border border-slate-200">
        <table className="min-w-full divide-y divide-slate-200">
          <thead className="bg-slate-50">
            <tr>
              {["Kind", "Version", "Title", "Hash", "Effective", "Status", ""].map((h) => (
                <th key={h} className="px-4 py-2 text-left text-xs font-semibold uppercase tracking-wide text-slate-600">{h}</th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100 bg-white">
            {versions.map((p) => (
              <tr key={p.id}>
                <td className="px-4 py-2 text-xs text-slate-700">{p.kind}</td>
                <td className="px-4 py-2 text-sm font-medium">{p.version}</td>
                <td className="px-4 py-2 text-sm">{p.title}</td>
                <td className="px-4 py-2 font-mono text-[11px] text-slate-500">{p.contentHash.slice(0, 12)}…</td>
                <td className="px-4 py-2 text-xs text-slate-600">{new Date(p.effectiveAtUtc).toLocaleString()}</td>
                <td className="px-4 py-2">
                  <span className={`rounded-full border px-2 py-0.5 text-xs ${p.isActive
                      ? "border-emerald-200 bg-emerald-50 text-emerald-700"
                      : "border-slate-200 bg-slate-100 text-slate-500"}`}>
                    {p.isActive ? "Active" : "Retired"}
                  </span>
                </td>
                <td className="px-4 py-2 text-right">
                  <button onClick={() => setReading(p)} className="text-xs text-blue-700 hover:underline">View</button>
                </td>
              </tr>
            ))}
            {!loading && versions.length === 0 && (
              <tr><td colSpan={7} className="px-4 py-6 text-center text-sm text-slate-500">No policy versions published yet.</td></tr>
            )}
          </tbody>
        </table>
      </div>

      {composerOpen && (<ComposerModal onClose={() => setComposerOpen(false)} onPublished={() => { setComposerOpen(false); reload(); }} />)}
      {reading && (<ReaderModal policy={reading} onClose={() => setReading(null)} />)}
    </div>
  );
}

function ComposerModal({ onClose, onPublished }: { onClose: () => void; onPublished: () => void }) {
  const [kind, setKind] = useState<PolicyDocumentKind>("TermsOfService");
  const [version, setVersion] = useState(`v${new Date().toISOString().slice(0, 10)}`);
  const [title, setTitle] = useState("");
  const [body, setBody] = useState("");
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true); setErr(null);
    try {
      await publishPolicyVersion({ kind, version: version.trim(), title: title.trim(), bodyMarkdown: body });
      onPublished();
    } catch (ex) { setErr((ex as Error).message); }
    finally { setBusy(false); }
  }

  return (
    <div className="fixed inset-0 z-40 flex items-center justify-center bg-slate-900/40 p-4">
      <form onSubmit={submit} className="w-full max-w-3xl space-y-4 rounded-xl bg-white p-6 shadow-xl">
        <header className="flex items-center justify-between">
          <h2 className="text-lg font-semibold">Publish new policy version</h2>
          <button type="button" onClick={onClose} className="text-slate-500 hover:text-slate-800">Close</button>
        </header>
        {err && (<div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{err}</div>)}

        <div className="grid grid-cols-3 gap-3">
          <div className="space-y-2">
            <label className="block text-xs font-medium uppercase tracking-wide text-slate-600">Kind</label>
            <select className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm" value={kind} onChange={(e) => setKind(e.target.value as PolicyDocumentKind)}>
              {POLICY_KINDS.map((k) => (<option key={k} value={k}>{k}</option>))}
            </select>
          </div>
          <div className="space-y-2 col-span-2">
            <label className="block text-xs font-medium uppercase tracking-wide text-slate-600">Version</label>
            <input className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm" required value={version} onChange={(e) => setVersion(e.target.value)} />
          </div>
        </div>

        <div className="space-y-2">
          <label className="block text-xs font-medium uppercase tracking-wide text-slate-600">Title</label>
          <input className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm" required value={title} onChange={(e) => setTitle(e.target.value)} />
        </div>

        <div className="space-y-2">
          <label className="block text-xs font-medium uppercase tracking-wide text-slate-600">Body (Markdown)</label>
          <textarea className="w-full rounded-md border border-slate-300 px-3 py-2 font-mono text-sm" rows={14} required value={body} onChange={(e) => setBody(e.target.value)} />
        </div>

        <div className="flex justify-end gap-2">
          <button type="button" onClick={onClose} className="rounded-md border border-slate-300 px-3 py-2 text-sm">Cancel</button>
          <button type="submit" disabled={busy} className="rounded-md bg-slate-900 px-3 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-50">
            {busy ? "Publishing…" : "Publish"}
          </button>
        </div>
      </form>
    </div>
  );
}

function ReaderModal({ policy, onClose }: { policy: PolicyDocumentDto; onClose: () => void }) {
  return (
    <div className="fixed inset-0 z-40 flex items-center justify-center bg-slate-900/40 p-4">
      <div className="w-full max-w-3xl space-y-3 rounded-xl bg-white p-6 shadow-xl">
        <header className="flex items-center justify-between">
          <div>
            <h2 className="text-lg font-semibold">{policy.title}</h2>
            <p className="text-xs text-slate-500">
              {policy.kind} · {policy.version} · hash <span className="font-mono">{policy.contentHash}</span>
            </p>
          </div>
          <button onClick={onClose} className="text-slate-500 hover:text-slate-800">Close</button>
        </header>
        <pre className="max-h-[60vh] overflow-auto whitespace-pre-wrap rounded-md bg-slate-50 p-4 font-mono text-xs text-slate-800">
          {policy.bodyMarkdown}
        </pre>
      </div>
    </div>
  );
}
