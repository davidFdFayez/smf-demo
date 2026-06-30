import { useCallback, useEffect, useState } from "react";
import {
  GOVERNANCE_DOC_TYPES,
  deleteGovernanceDocument,
  listGovernanceDocuments,
  publishGovernanceDocument,
  uploadGovernanceDocument,
  type GovernanceDocumentDto,
  type GovernanceDocumentType,
} from "../services/complianceApi";

/**
 * Admin console for SOPC-required transparency documents (annual reports,
 * anti-doping policy, athlete-protection rules, etc.). Lets ops upload new
 * versions, edit metadata, and toggle publication so the public site only
 * surfaces vetted copies.
 */
export function GovernanceDocumentsPage() {
  const [docs, setDocs] = useState<GovernanceDocumentDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [showUpload, setShowUpload] = useState(false);
  const [typeFilter, setTypeFilter] = useState<GovernanceDocumentType | "">("");

  const reload = useCallback(async () => {
    setLoading(true); setError(null);
    try {
      setDocs(await listGovernanceDocuments(typeFilter ? { type: typeFilter } : undefined));
    } catch (err) { setError((err as Error).message); }
    finally { setLoading(false); }
  }, [typeFilter]);

  useEffect(() => { reload(); }, [reload]);

  async function togglePublish(d: GovernanceDocumentDto) {
    setBusyId(d.id);
    try {
      await publishGovernanceDocument(d.id, !d.isPublished);
      await reload();
    } catch (err) { alert((err as Error).message); }
    finally { setBusyId(null); }
  }

  async function remove(d: GovernanceDocumentDto) {
    if (!confirm(`Delete '${d.title}'? This will remove the file from storage.`)) return;
    setBusyId(d.id);
    try {
      await deleteGovernanceDocument(d.id);
      await reload();
    } catch (err) { alert((err as Error).message); }
    finally { setBusyId(null); }
  }

  return (
    <div className="space-y-6 p-6">
      <header className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Governance documents</h1>
          <p className="text-sm text-slate-500">
            Annual reports, anti-doping policies, athlete-protection statutes and other SOPC-compliant records.
          </p>
        </div>
        <button
          className="rounded-lg bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800"
          onClick={() => setShowUpload(true)}
        >
          Upload document
        </button>
      </header>

      <div className="flex flex-wrap items-center gap-3">
        <select
          className="rounded-md border border-slate-300 px-3 py-1.5 text-sm"
          value={typeFilter}
          onChange={(e) => setTypeFilter((e.target.value || "") as GovernanceDocumentType | "")}
        >
          <option value="">All types</option>
          {GOVERNANCE_DOC_TYPES.map((t) => (<option key={t} value={t}>{t}</option>))}
        </select>
        <button onClick={reload} className="rounded-md border border-slate-300 px-3 py-1.5 text-sm hover:bg-slate-50">
          Refresh
        </button>
      </div>

      {error && (<div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{error}</div>)}
      {loading && (<div className="text-sm text-slate-500">Loading…</div>)}

      <div className="overflow-x-auto rounded-lg border border-slate-200">
        <table className="min-w-full divide-y divide-slate-200">
          <thead className="bg-slate-50">
            <tr>
              {["Title", "Type", "Year", "File", "Size", "Status", "Uploaded", ""].map((h) => (
                <th key={h} className="px-4 py-2 text-left text-xs font-semibold uppercase tracking-wide text-slate-600">{h}</th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100 bg-white">
            {docs.map((d) => (
              <tr key={d.id}>
                <td className="px-4 py-2 text-sm font-medium">{d.title}</td>
                <td className="px-4 py-2 text-xs text-slate-600">{d.documentType}</td>
                <td className="px-4 py-2 text-xs text-slate-600">{d.coveringYear ?? "—"}</td>
                <td className="px-4 py-2 text-xs">
                  <a className="text-blue-700 hover:underline" href={d.downloadUrl} target="_blank" rel="noreferrer">
                    {d.originalFileName}
                  </a>
                </td>
                <td className="px-4 py-2 text-xs text-slate-600">{(d.fileSizeBytes / 1024).toFixed(1)} KB</td>
                <td className="px-4 py-2">
                  <span className={`rounded-full border px-2 py-0.5 text-xs ${d.isPublished
                      ? "border-emerald-200 bg-emerald-50 text-emerald-700"
                      : "border-slate-200 bg-slate-100 text-slate-600"}`}>
                    {d.isPublished ? "Published" : "Draft"}
                  </span>
                </td>
                <td className="px-4 py-2 text-xs text-slate-500">{new Date(d.uploadedAtUtc).toLocaleString()}</td>
                <td className="px-4 py-2 text-right text-xs">
                  <button
                    className="mr-2 rounded border border-slate-300 px-2 py-1 hover:bg-slate-50 disabled:opacity-50"
                    disabled={busyId === d.id}
                    onClick={() => togglePublish(d)}
                  >{d.isPublished ? "Unpublish" : "Publish"}</button>
                  <button
                    className="rounded border border-red-300 px-2 py-1 text-red-600 hover:bg-red-50 disabled:opacity-50"
                    disabled={busyId === d.id}
                    onClick={() => remove(d)}
                  >Delete</button>
                </td>
              </tr>
            ))}
            {!loading && docs.length === 0 && (
              <tr><td className="px-4 py-6 text-center text-sm text-slate-500" colSpan={8}>No governance documents yet.</td></tr>
            )}
          </tbody>
        </table>
      </div>

      {showUpload && (<UploadModal onClose={() => setShowUpload(false)} onUploaded={() => { setShowUpload(false); reload(); }} />)}
    </div>
  );
}

function UploadModal({ onClose, onUploaded }: { onClose: () => void; onUploaded: () => void }) {
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [docType, setDocType] = useState<GovernanceDocumentType>("AnnualReport");
  const [year, setYear] = useState<string>(String(new Date().getFullYear()));
  const [file, setFile] = useState<File | null>(null);
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!file) { setErr("Please select a file."); return; }
    setBusy(true); setErr(null);
    try {
      await uploadGovernanceDocument({
        title: title.trim(), description: description.trim() || undefined,
        documentType: docType, coveringYear: year ? Number(year) : undefined, file,
      });
      onUploaded();
    } catch (ex) { setErr((ex as Error).message); }
    finally { setBusy(false); }
  }

  return (
    <div className="fixed inset-0 z-40 flex items-center justify-center bg-slate-900/40 p-4">
      <form onSubmit={submit} className="w-full max-w-lg space-y-4 rounded-xl bg-white p-6 shadow-xl">
        <header className="flex items-center justify-between">
          <h2 className="text-lg font-semibold">Upload governance document</h2>
          <button type="button" onClick={onClose} className="text-slate-500 hover:text-slate-800">Close</button>
        </header>

        {err && (<div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{err}</div>)}

        <div className="space-y-2">
          <label className="block text-xs font-medium uppercase tracking-wide text-slate-600">Title</label>
          <input className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm" required value={title} onChange={(e) => setTitle(e.target.value)} />
        </div>

        <div className="space-y-2">
          <label className="block text-xs font-medium uppercase tracking-wide text-slate-600">Description</label>
          <textarea className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm" rows={3} value={description} onChange={(e) => setDescription(e.target.value)} />
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-2">
            <label className="block text-xs font-medium uppercase tracking-wide text-slate-600">Document type</label>
            <select className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm" value={docType} onChange={(e) => setDocType(e.target.value as GovernanceDocumentType)}>
              {GOVERNANCE_DOC_TYPES.map((t) => (<option key={t} value={t}>{t}</option>))}
            </select>
          </div>
          <div className="space-y-2">
            <label className="block text-xs font-medium uppercase tracking-wide text-slate-600">Covering year</label>
            <input className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm" type="number" min={1900} max={2200} value={year} onChange={(e) => setYear(e.target.value)} />
          </div>
        </div>

        <div className="space-y-2">
          <label className="block text-xs font-medium uppercase tracking-wide text-slate-600">File (PDF/Office/PNG/JPEG)</label>
          <input type="file" required onChange={(e) => setFile(e.target.files?.[0] ?? null)} className="block w-full text-sm" />
        </div>

        <div className="flex justify-end gap-2 pt-2">
          <button type="button" className="rounded-md border border-slate-300 px-3 py-2 text-sm" onClick={onClose}>Cancel</button>
          <button type="submit" disabled={busy} className="rounded-md bg-slate-900 px-3 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-50">
            {busy ? "Uploading…" : "Upload"}
          </button>
        </div>
      </form>
    </div>
  );
}
