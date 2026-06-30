import { useState } from "react";
import clsx from "clsx";
import {
  certificateDownloadUrl,
  issueCertificate,
  listMemberCertificates,
  verifyCertificate,
  type CertificateSummary,
  type CertificateType,
  type IssueCertificateInput,
} from "../services/certificatesApi";

const TYPES: CertificateType[] = [
  "Membership",
  "Rank",
  "RefereeLicense",
  "CoachLicense",
  "MedicalClearance",
  "EventParticipation",
  "CourseCompletion",
];

/**
 * Certificate operator screen. Three panels:
 *   1. Issue a certificate for a member (wraps POST /api/certificates).
 *   2. List every certificate owned by a member (by-member lookup).
 *   3. Verify a public verification code — exactly what the QR code in
 *      the issued certificate PDF hits, minus the public wrapper page.
 *
 * "Download" opens the protected PDF/HTML endpoint in a new tab.
 */
export function CertificatesPage() {
  return (
    <div className="grid gap-5 xl:grid-cols-2">
      <IssuePanel />
      <LookupPanel />
      <VerifyPanel />
    </div>
  );
}

// ── Issue ────────────────────────────────────────────────────────────────────

function IssuePanel() {
  const [form, setForm] = useState<IssueCertificateInput>({
    memberId: "",
    type: "Membership",
    title: "",
    issuingAuthority: "",
    expiresAtUtc: "",
  });
  const [result, setResult] = useState<CertificateSummary | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setErr(null);
    try {
      const expires = form.expiresAtUtc ? new Date(form.expiresAtUtc).toISOString() : null;
      const saved = await issueCertificate({
        memberId: form.memberId,
        type: form.type,
        title: form.title,
        issuingAuthority: form.issuingAuthority || null,
        expiresAtUtc: expires,
      });
      setResult(saved);
    } catch (e) {
      setErr((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="card p-5">
      <h3 className="text-sm font-semibold text-slate-900">Issue certificate</h3>
      <form onSubmit={submit} className="mt-4 grid gap-3">
        <label className="block">
          <span className="admin-label">Member ID</span>
          <input
            required
            value={form.memberId}
            onChange={(e) => setForm((f) => ({ ...f, memberId: e.target.value }))}
            className="admin-input mt-1 font-mono text-xs"
          />
        </label>
        <div className="grid gap-3 sm:grid-cols-2">
          <label className="block">
            <span className="admin-label">Type</span>
            <select
              value={form.type}
              onChange={(e) => setForm((f) => ({ ...f, type: e.target.value as CertificateType }))}
              className="admin-input mt-1"
            >
              {TYPES.map((t) => (
                <option key={t} value={t}>
                  {t}
                </option>
              ))}
            </select>
          </label>
          <label className="block">
            <span className="admin-label">Expires (optional)</span>
            <input
              type="date"
              value={form.expiresAtUtc ?? ""}
              onChange={(e) => setForm((f) => ({ ...f, expiresAtUtc: e.target.value }))}
              className="admin-input mt-1"
            />
          </label>
        </div>
        <label className="block">
          <span className="admin-label">Title</span>
          <input
            required
            value={form.title}
            onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
            className="admin-input mt-1"
          />
        </label>
        <label className="block">
          <span className="admin-label">Issuing authority (optional)</span>
          <input
            value={form.issuingAuthority ?? ""}
            onChange={(e) => setForm((f) => ({ ...f, issuingAuthority: e.target.value }))}
            className="admin-input mt-1"
          />
        </label>
        <div className="flex items-center gap-3">
          <button type="submit" disabled={busy} className="btn-primary">
            {busy ? "Issuing…" : "Issue"}
          </button>
          {err && <span className="text-xs text-rose-700">{err}</span>}
        </div>
      </form>
      {result && <CertRow cert={result} />}
    </div>
  );
}

// ── Lookup ───────────────────────────────────────────────────────────────────

function LookupPanel() {
  const [memberId, setMemberId] = useState("");
  const [certs, setCerts] = useState<CertificateSummary[] | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const run = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setErr(null);
    try {
      setCerts(await listMemberCertificates(memberId));
    } catch (e) {
      setCerts(null);
      setErr((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="card p-5">
      <h3 className="text-sm font-semibold text-slate-900">By member</h3>
      <form onSubmit={run} className="mt-4 flex items-end gap-2">
        <label className="flex-1">
          <span className="admin-label">Member ID</span>
          <input
            required
            value={memberId}
            onChange={(e) => setMemberId(e.target.value)}
            className="admin-input mt-1 font-mono text-xs"
          />
        </label>
        <button type="submit" disabled={busy} className="btn-secondary">
          {busy ? "Loading…" : "List"}
        </button>
      </form>
      {err && <div className="mt-3 text-xs text-rose-700">{err}</div>}
      {certs && certs.length === 0 && (
        <div className="mt-3 text-xs text-slate-500">No certificates issued to this member.</div>
      )}
      {certs && certs.length > 0 && (
        <ul className="mt-3 space-y-2">
          {certs.map((c) => (
            <CertRow key={c.id} cert={c} />
          ))}
        </ul>
      )}
    </div>
  );
}

// ── Verify ───────────────────────────────────────────────────────────────────

function VerifyPanel() {
  const [code, setCode] = useState("");
  const [cert, setCert] = useState<CertificateSummary | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const run = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setErr(null);
    setCert(null);
    try {
      setCert(await verifyCertificate(code));
    } catch (e) {
      setErr((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="card p-5 xl:col-span-2">
      <h3 className="text-sm font-semibold text-slate-900">Verify code</h3>
      <p className="text-xs text-slate-500">
        Same endpoint a public scanner hits — use it to troubleshoot a QR code.
      </p>
      <form onSubmit={run} className="mt-4 flex items-end gap-2">
        <label className="flex-1">
          <span className="admin-label">Verification code</span>
          <input
            required
            value={code}
            onChange={(e) => setCode(e.target.value)}
            className="admin-input mt-1 font-mono text-xs"
          />
        </label>
        <button type="submit" disabled={busy} className="btn-primary">
          {busy ? "Checking…" : "Verify"}
        </button>
      </form>
      {err && <div className="mt-3 text-xs text-rose-700">{err}</div>}
      {cert && <CertRow cert={cert} />}
    </div>
  );
}

// ── Row ──────────────────────────────────────────────────────────────────────

function CertRow({ cert }: { cert: CertificateSummary }) {
  const expired = cert.expiresAtUtc ? new Date(cert.expiresAtUtc).getTime() < Date.now() : false;
  const invalid = cert.isRevoked || expired;
  return (
    <li className="mt-4 rounded-xl border border-slate-200 bg-slate-50 p-3 first:mt-0">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex items-center gap-2 text-sm font-semibold text-slate-900">
            <span className="truncate">{cert.title}</span>
            <span
              className={clsx(
                "inline-flex rounded-full px-2 py-0.5 text-[10px] font-semibold ring-1 ring-inset",
                invalid
                  ? "bg-rose-50 text-rose-700 ring-rose-500/30"
                  : "bg-emerald-50 text-emerald-700 ring-emerald-500/30",
              )}
            >
              {cert.isRevoked ? "Revoked" : expired ? "Expired" : "Valid"}
            </span>
          </div>
          <div className="text-[11px] text-slate-500">
            {cert.type} · {cert.issuingAuthority ?? "—"} · Issued{" "}
            {new Date(cert.issuedAtUtc).toLocaleDateString()}
          </div>
          <div className="mt-1 font-mono text-[11px] text-slate-500">
            {cert.verificationCode}
          </div>
        </div>
        <a
          href={certificateDownloadUrl(cert.id)}
          target="_blank"
          rel="noopener noreferrer"
          className="btn-secondary text-xs"
        >
          Download
        </a>
      </div>
    </li>
  );
}
