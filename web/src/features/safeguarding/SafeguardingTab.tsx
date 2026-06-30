import { useState } from "react";
import {
  submitSafeguardingReport,
  type SafeguardingCategory,
  type SafeguardingReportDetails,
  type SubmitReportInput,
} from "../../services/safeguardingApi";

const CATEGORIES: { id: SafeguardingCategory; label: string }[] = [
  { id: "AntiDoping",   label: "Anti-doping violation" },
  { id: "Safeguarding", label: "Safeguarding concern (minor or vulnerable athlete)" },
  { id: "Harassment",   label: "Harassment or discrimination" },
  { id: "MatchFixing",  label: "Match-fixing or integrity issue" },
  { id: "Other",        label: "Other" },
];

const defaultInput: SubmitReportInput = {
  category: "Safeguarding",
  subject: "",
  description: "",
  incidentLocation: "",
  incidentDate: "",
  isAnonymous: true,
  reporterName: "",
  reporterEmail: "",
  reporterPhone: "",
};

export function SafeguardingTab() {
  const [input, setInput] = useState<SubmitReportInput>(defaultInput);
  const [submitting, setSubmitting] = useState(false);
  const [result, setResult] = useState<SafeguardingReportDetails | null>(null);
  const [error, setError] = useState<string | null>(null);

  const update = <K extends keyof SubmitReportInput>(k: K, v: SubmitReportInput[K]) =>
    setInput((s) => ({ ...s, [k]: v }));

  const submit = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      const payload: SubmitReportInput = {
        ...input,
        incidentLocation: input.incidentLocation?.trim() || undefined,
        incidentDate: input.incidentDate?.trim() || undefined,
        reporterName: input.isAnonymous ? undefined : input.reporterName?.trim() || undefined,
        reporterEmail: input.isAnonymous ? undefined : input.reporterEmail?.trim() || undefined,
        reporterPhone: input.isAnonymous ? undefined : input.reporterPhone?.trim() || undefined,
      };
      const res = await submitSafeguardingReport(payload);
      setResult(res);
      setInput(defaultInput);
    } catch {
      setError(
        "Could not submit report. If you're not anonymous, make sure " +
        "at least email or phone is filled in.",
      );
    } finally {
      setSubmitting(false);
    }
  };

  if (result) {
    return (
      <section className="space-y-4">
        <div className="rounded-xl border border-emerald-200 bg-emerald-50 p-5">
          <h3 className="text-lg font-semibold text-emerald-900">
            Thank you — your report has been received.
          </h3>
          <p className="mt-2 text-sm text-emerald-900">
            The federation's safeguarding officer has been notified. Please keep
            your reference code somewhere safe — you can use it to check on the
            status of this report later without identifying yourself.
          </p>
          <div className="mt-4 rounded-lg border border-emerald-300 bg-white p-4">
            <div className="text-xs uppercase tracking-wide text-emerald-700">
              Reference code
            </div>
            <div className="mt-1 font-mono text-lg text-slate-900">
              {result.referenceCode}
            </div>
          </div>
        </div>
        <button
          type="button"
          onClick={() => setResult(null)}
          className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
        >
          File another report
        </button>
      </section>
    );
  }

  return (
    <section className="space-y-6">
      <div className="rounded-xl border border-slate-200 bg-slate-50 p-4 text-sm text-slate-700">
        <p>
          <strong>Confidential.</strong> Use this form to report suspected
          anti-doping violations, safeguarding concerns involving minors or
          vulnerable athletes, harassment, match-fixing, or other integrity
          issues. Reports go directly to the federation's safeguarding
          officer. You may submit anonymously.
        </p>
      </div>

      <form onSubmit={submit} className="space-y-4 rounded-xl border border-slate-200 bg-white p-5">
        <label className="block text-sm">
          <span className="mb-1 block text-slate-700">What are you reporting?</span>
          <select
            value={input.category}
            onChange={(e) => update("category", e.target.value as SafeguardingCategory)}
            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2"
          >
            {CATEGORIES.map((c) => <option key={c.id} value={c.id}>{c.label}</option>)}
          </select>
        </label>

        <label className="block text-sm">
          <span className="mb-1 block text-slate-700">Subject</span>
          <input
            value={input.subject}
            onChange={(e) => update("subject", e.target.value)}
            required maxLength={200}
            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2"
          />
        </label>

        <label className="block text-sm">
          <span className="mb-1 block text-slate-700">Description</span>
          <textarea
            value={input.description}
            onChange={(e) => update("description", e.target.value)}
            required minLength={10} maxLength={5000} rows={6}
            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2"
          />
        </label>

        <div className="grid gap-4 md:grid-cols-2">
          <label className="block text-sm">
            <span className="mb-1 block text-slate-700">Incident location (optional)</span>
            <input
              value={input.incidentLocation ?? ""}
              onChange={(e) => update("incidentLocation", e.target.value)}
              maxLength={200}
              className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2"
            />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-slate-700">Incident date (optional)</span>
            <input
              type="date"
              value={input.incidentDate ?? ""}
              onChange={(e) => update("incidentDate", e.target.value)}
              className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2"
            />
          </label>
        </div>

        <label className="flex items-center gap-2 text-sm text-slate-700">
          <input
            type="checkbox"
            checked={input.isAnonymous}
            onChange={(e) => update("isAnonymous", e.target.checked)}
          />
          Submit anonymously (the federation will not be able to follow up with you)
        </label>

        {!input.isAnonymous && (
          <div className="grid gap-4 rounded-lg border border-slate-200 bg-slate-50 p-4 md:grid-cols-3">
            <label className="block text-sm">
              <span className="mb-1 block text-slate-700">Your name</span>
              <input
                value={input.reporterName ?? ""}
                onChange={(e) => update("reporterName", e.target.value)}
                className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2"
              />
            </label>
            <label className="block text-sm">
              <span className="mb-1 block text-slate-700">Email</span>
              <input
                type="email"
                value={input.reporterEmail ?? ""}
                onChange={(e) => update("reporterEmail", e.target.value)}
                className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2"
              />
            </label>
            <label className="block text-sm">
              <span className="mb-1 block text-slate-700">Phone</span>
              <input
                value={input.reporterPhone ?? ""}
                onChange={(e) => update("reporterPhone", e.target.value)}
                className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2"
              />
            </label>
          </div>
        )}

        {error && (
          <div className="rounded-lg border border-red-300 bg-red-50 px-3 py-2 text-sm text-red-800">
            {error}
          </div>
        )}

        <button
          type="submit"
          disabled={submitting}
          className="rounded-lg bg-smf-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-smf-700 disabled:opacity-60"
        >
          {submitting ? "Submitting…" : "Submit report"}
        </button>
      </form>
    </section>
  );
}
