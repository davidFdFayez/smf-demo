import { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import {
  decideGuardianTask,
  getGuardianTask,
  type GuardianTaskDto,
} from "../services/complianceApi";

/**
 * Public guardian sign-in landing page. The federation emails a unique URL
 * containing the consent id + a one-time token; the guardian opens it and
 * either approves (digital signature) or declines the registration. All
 * decisions are HMAC-signed and stored alongside IP/UA in the audit log.
 */
export function GuardianSignPage() {
  const { consentId, token } = useParams<{ consentId: string; token: string }>();
  const [task, setTask] = useState<GuardianTaskDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [decision, setDecision] = useState<"approved" | "declined" | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [reason, setReason] = useState("");
  const [agreed, setAgreed] = useState(false);

  useEffect(() => {
    if (!consentId || !token) { setLoading(false); return; }
    let cancelled = false;
    setLoading(true); setError(null);
    getGuardianTask(consentId, token)
      .then((t) => { if (!cancelled) setTask(t); })
      .catch((err) => { if (!cancelled) setError((err as Error).message); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [consentId, token]);

  async function approve() {
    if (!consentId || !token) return;
    setSubmitting(true); setError(null);
    try {
      await decideGuardianTask(consentId, token, true);
      setDecision("approved");
    } catch (err) { setError((err as Error).message); }
    finally { setSubmitting(false); }
  }

  async function decline() {
    if (!consentId || !token) return;
    setSubmitting(true); setError(null);
    try {
      await decideGuardianTask(consentId, token, false, reason.trim() || undefined);
      setDecision("declined");
    } catch (err) { setError((err as Error).message); }
    finally { setSubmitting(false); }
  }

  if (loading) {
    return <div className="mx-auto max-w-2xl px-4 py-10 text-sm text-slate-500">Loading consent ceremony…</div>;
  }

  if (!task) {
    return (
      <div className="mx-auto max-w-2xl px-4 py-10">
        <div className="rounded-lg border border-red-300 bg-red-50 px-4 py-6 text-sm text-red-800">
          <h2 className="text-base font-semibold">Link not found</h2>
          <p className="mt-1">
            This consent link is invalid, has already been used, or was generated for a different
            registration. Please contact the federation if you believe this is an error.
          </p>
        </div>
      </div>
    );
  }

  if (decision === "approved") {
    return (
      <ResultCard tone="success" title="Thank you — registration approved.">
        Your digital signature has been recorded. {task.memberName}'s registration is now active.
        A copy of the signed consent has been logged for SOPC audit purposes.
      </ResultCard>
    );
  }

  if (decision === "declined") {
    return (
      <ResultCard tone="warning" title="Registration declined.">
        We've recorded your decision. The federation will not activate {task.memberName}'s
        membership.
      </ResultCard>
    );
  }

  if (task.alreadyDecided) {
    return <ResultCard tone="info" title="Already decided">This consent ceremony has already been completed.</ResultCard>;
  }
  if (task.isExpired) {
    return (
      <ResultCard tone="warning" title="Link expired">
        This consent link has expired. Please contact the federation to issue a new one.
      </ResultCard>
    );
  }

  const dob = new Date(task.memberDateOfBirth).toLocaleDateString();

  return (
    <div className="mx-auto max-w-2xl space-y-6 px-4 py-8">
      <header>
        <h1 className="text-2xl font-bold text-slate-900">Parental consent</h1>
        <p className="mt-1 text-sm text-slate-600">
          You've been asked to sign the federation registration for{" "}
          <span className="font-semibold">{task.memberName}</span> (DOB {dob}).
        </p>
      </header>

      <div className="rounded-lg border border-slate-200 bg-white p-4 text-sm shadow-sm">
        <dl className="grid grid-cols-2 gap-y-2 text-slate-700">
          <dt className="font-medium">Guardian:</dt>
          <dd>{task.guardianFullName} ({task.relation})</dd>
          <dt className="font-medium">Link expires:</dt>
          <dd>{new Date(task.tokenExpiresAtUtc).toLocaleString()}</dd>
          {task.policy && (
            <>
              <dt className="font-medium">Consent version:</dt>
              <dd className="font-mono">{task.policy.version}</dd>
            </>
          )}
        </dl>
      </div>

      {task.policy && (
        <article className="max-h-80 overflow-auto rounded-lg border border-slate-200 bg-slate-50 p-4">
          <h2 className="text-base font-semibold text-slate-800">{task.policy.title}</h2>
          <pre className="mt-2 whitespace-pre-wrap font-sans text-sm leading-relaxed text-slate-700">
            {task.policy.bodyMarkdown}
          </pre>
        </article>
      )}

      <label className="flex items-start gap-3 text-sm text-slate-700">
        <input
          type="checkbox"
          className="mt-1 h-4 w-4 rounded border-slate-300"
          checked={agreed}
          onChange={(e) => setAgreed(e.target.checked)}
        />
        <span>
          I confirm I am the legal guardian of {task.memberName} and I have read the consent terms.
          By approving I am providing a binding digital signature on behalf of the minor.
        </span>
      </label>

      {error && (
        <div className="rounded-md border border-red-300 bg-red-50 px-3 py-2 text-sm text-red-700">{error}</div>
      )}

      <div className="flex flex-col gap-3 sm:flex-row">
        <button
          type="button"
          onClick={approve}
          disabled={submitting || !agreed}
          className="rounded-md bg-emerald-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-emerald-700 disabled:cursor-not-allowed disabled:bg-slate-300"
        >
          {submitting ? "Signing…" : "Approve & digitally sign"}
        </button>

        <details className="flex-1 rounded-md border border-slate-200 bg-white p-3 text-sm">
          <summary className="cursor-pointer font-medium text-slate-700">Decline registration</summary>
          <div className="mt-2 space-y-2">
            <textarea
              className="field-input w-full text-sm"
              rows={3}
              placeholder="Optional reason (helps the federation follow up)"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
            />
            <button
              type="button"
              onClick={decline}
              disabled={submitting}
              className="rounded-md border border-red-400 px-3 py-1.5 text-xs font-semibold text-red-700 hover:bg-red-50 disabled:opacity-60"
            >
              {submitting ? "Submitting…" : "Confirm decline"}
            </button>
          </div>
        </details>
      </div>
    </div>
  );
}

function ResultCard({
  tone, title, children,
}: { tone: "success" | "warning" | "info"; title: string; children: React.ReactNode }) {
  const cls = {
    success: "border-emerald-300 bg-emerald-50 text-emerald-900",
    warning: "border-amber-300 bg-amber-50 text-amber-900",
    info:    "border-slate-300 bg-slate-50 text-slate-800",
  }[tone];
  return (
    <div className="mx-auto max-w-2xl px-4 py-10">
      <div className={`rounded-lg border px-5 py-6 text-sm ${cls}`}>
        <h2 className="text-base font-semibold">{title}</h2>
        <p className="mt-1">{children}</p>
      </div>
    </div>
  );
}
