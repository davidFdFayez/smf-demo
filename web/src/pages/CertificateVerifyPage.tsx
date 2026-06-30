import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import {
  certificateDownloadUrl,
  verifyCertificate,
  type CertificateSummary,
} from "../services/certificatesApi";

/**
 * Public certificate verification page — the URL the QR code in the
 * generated PDF / HTML certificate points at.
 *
 *   /certificates/verify/:code
 *
 * The page fetches `/api/certificates/verify/:code`, which is an unauthenticated
 * endpoint returning a `CertificateSummary`. We render its validity state
 * plus a "Download original" button that links to the protected download URL.
 *
 * When no code is present in the URL we render a small lookup form so a user
 * with a physical certificate can type their code in manually — useful when
 * the QR code is unreadable (fax, photocopy, etc).
 */
export function CertificateVerifyPage() {
  const { code } = useParams<{ code?: string }>();
  const nav = useNavigate();
  const [input, setInput] = useState(code ?? "");
  const [cert, setCert] = useState<CertificateSummary | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(Boolean(code));

  useEffect(() => {
    if (!code) {
      setLoading(false);
      setCert(null);
      return;
    }
    const ctrl = new AbortController();
    setLoading(true);
    setError(null);
    verifyCertificate(code, ctrl.signal)
      .then(setCert)
      .catch((err) => {
        setCert(null);
        setError(
          err?.response?.status === 404
            ? "No certificate found for this code."
            : "Failed to verify certificate.",
        );
      })
      .finally(() => setLoading(false));
    return () => ctrl.abort();
  }, [code]);

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    const c = input.trim();
    if (c.length === 0) return;
    nav(`/certificates/verify/${encodeURIComponent(c)}`);
  };

  return (
    <section className="mx-auto max-w-3xl px-6 py-12">
      <h1 className="text-3xl font-bold tracking-tight text-slate-900">
        Certificate verification
      </h1>
      <p className="mt-2 text-slate-600">
        Scan the QR code on an issued certificate, or enter the verification
        code printed next to it.
      </p>

      <form onSubmit={submit} className="mt-6 flex flex-col gap-2 sm:flex-row">
        <input
          value={input}
          onChange={(e) => setInput(e.target.value)}
          placeholder="Verification code"
          className="flex-1 rounded-lg border-slate-300 bg-white px-3 py-2 font-mono text-sm shadow-sm focus:border-smf-500 focus:ring-smf-500"
        />
        <button
          type="submit"
          className="rounded-lg bg-smf-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-smf-700"
        >
          Verify
        </button>
      </form>

      {loading && <p className="mt-8 text-sm text-slate-500">Checking…</p>}

      {error && (
        <div className="mt-8 rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-800">
          {error}
          <div className="mt-2 text-xs text-rose-700/80">
            If you believe this certificate is valid, please contact the
            federation with the code above.
          </div>
        </div>
      )}

      {cert && <CertificateCard cert={cert} />}

      <div className="mt-10 text-xs text-slate-500">
        <Link to="/" className="hover:underline">
          ← Back to homepage
        </Link>
      </div>
    </section>
  );
}

function CertificateCard({ cert }: { cert: CertificateSummary }) {
  const issued = new Date(cert.issuedAtUtc).toLocaleDateString();
  const expires = cert.expiresAtUtc
    ? new Date(cert.expiresAtUtc).toLocaleDateString()
    : null;
  const expired =
    expires != null && new Date(cert.expiresAtUtc!).getTime() < Date.now();

  const invalid = cert.isRevoked || expired;

  return (
    <div
      className={`mt-8 overflow-hidden rounded-2xl border ${
        invalid
          ? "border-rose-200 bg-rose-50/50"
          : "border-emerald-200 bg-emerald-50/40"
      }`}
    >
      <div
        className={`flex items-center justify-between px-5 py-3 text-white ${
          invalid ? "bg-rose-600" : "bg-emerald-600"
        }`}
      >
        <span className="text-sm font-semibold uppercase tracking-wide">
          {cert.isRevoked ? "Revoked" : expired ? "Expired" : "Valid"}
        </span>
        <span className="font-mono text-xs">{cert.verificationCode}</span>
      </div>

      <div className="grid gap-4 p-5 sm:grid-cols-2 sm:p-6">
        <dl className="space-y-3 text-sm">
          <Row label="Title" value={cert.title} />
          <Row label="Type" value={formatType(cert.type)} />
          {cert.issuingAuthority && (
            <Row label="Issuer" value={cert.issuingAuthority} />
          )}
          <Row label="Issued" value={issued} />
          <Row label="Expires" value={expires ?? "No expiry"} />
          <Row label="Member ID" value={cert.memberId} mono />
        </dl>

        <div className="self-end justify-self-end">
          <a
            href={certificateDownloadUrl(cert.id)}
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 shadow-sm hover:bg-slate-50"
          >
            Download original
          </a>
        </div>
      </div>
    </div>
  );
}

function Row({
  label,
  value,
  mono,
}: {
  label: string;
  value: string;
  mono?: boolean;
}) {
  return (
    <div className="flex items-start justify-between gap-6">
      <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">
        {label}
      </dt>
      <dd className={`${mono ? "font-mono text-xs" : "text-sm"} text-slate-800`}>
        {value}
      </dd>
    </div>
  );
}

function formatType(t: CertificateSummary["type"]): string {
  // Camel → spaced: "CourseCompletion" → "Course completion"
  const withSpaces = t.replace(/([A-Z])/g, " $1").trim();
  return withSpaces.charAt(0).toUpperCase() + withSpaces.slice(1).toLowerCase();
}
