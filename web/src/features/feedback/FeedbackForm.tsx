import { useState } from "react";
import {
  submitFeedback,
  type FeedbackSubjectType,
} from "../../services/communicationApi";

/**
 * Reusable feedback widget. Drop-in on any page that wants to capture
 * a 1-5 star rating plus comment. Both members and guests can submit;
 * comments enter the moderation queue (Pending) before going public.
 */
export function FeedbackForm({
  subjectType = "General",
  subjectId,
  defaultName = "",
  defaultEmail = "",
  memberId = null,
  title = "Share your feedback",
  description = "Tell us how the federation platform is working for you. Your review will appear after moderation.",
}: {
  subjectType?: FeedbackSubjectType;
  subjectId?: string;
  defaultName?: string;
  defaultEmail?: string;
  memberId?: string | null;
  title?: string;
  description?: string;
}) {
  const [rating,  setRating]  = useState(0);
  const [hover,   setHover]   = useState(0);
  const [comment, setComment] = useState("");
  const [name,    setName]    = useState(defaultName);
  const [email,   setEmail]   = useState(defaultEmail);
  const [busy,    setBusy]    = useState(false);
  const [error,   setError]   = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (rating < 1 || rating > 5) {
      setError("Please choose a rating from 1 to 5 stars.");
      return;
    }
    if (comment.trim().length < 5) {
      setError("Please leave a short comment (at least 5 characters).");
      return;
    }
    if (!name.trim()) {
      setError("Please tell us your name.");
      return;
    }

    setBusy(true); setError(null);
    try {
      await submitFeedback({
        subjectType,
        subjectId: subjectId ?? null,
        rating,
        comment: comment.trim(),
        authorMemberId: memberId,
        authorName: name.trim(),
        authorEmail: email.trim() || null,
      });
      setSuccess(true);
      setRating(0); setComment(""); setHover(0);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  }

  if (success) {
    return (
      <div className="rounded-2xl border border-emerald-200 bg-emerald-50 p-6 text-emerald-800">
        <h3 className="text-lg font-semibold">Thanks for your feedback!</h3>
        <p className="mt-1 text-sm">
          Our team reviews submissions within a couple of working days. Your
          rating helps us improve the federation platform for every member.
        </p>
        <button type="button" onClick={() => setSuccess(false)}
          className="mt-3 text-sm font-medium text-emerald-700 underline-offset-2 hover:underline">
          Submit another review
        </button>
      </div>
    );
  }

  return (
    <form onSubmit={submit} className="space-y-4 rounded-2xl border border-slate-200 bg-white p-6 shadow-card">
      <header>
        <h3 className="text-lg font-semibold text-slate-900">{title}</h3>
        <p className="mt-1 text-sm text-slate-500">{description}</p>
      </header>

      <div>
        <span className="block text-sm font-medium text-slate-700">Your rating</span>
        <div className="mt-1 flex items-center gap-1" role="radiogroup" aria-label="Star rating">
          {[1, 2, 3, 4, 5].map((n) => {
            const active = (hover || rating) >= n;
            return (
              <button
                key={n}
                type="button"
                role="radio"
                aria-checked={rating === n}
                onMouseEnter={() => setHover(n)}
                onMouseLeave={() => setHover(0)}
                onFocus={() => setHover(n)}
                onBlur={() => setHover(0)}
                onClick={() => setRating(n)}
                className={`text-3xl leading-none transition ${active ? "text-amber-400" : "text-slate-300"}`}
              >
                ★
              </button>
            );
          })}
          <span className="ms-2 text-sm text-slate-500">
            {rating > 0 ? `${rating} / 5` : "Tap a star"}
          </span>
        </div>
      </div>

      <label className="block text-sm">
        <span className="font-medium text-slate-700">Your review</span>
        <textarea
          value={comment}
          onChange={(e) => setComment(e.target.value)}
          rows={4}
          maxLength={2000}
          placeholder="What worked well? What could be better?"
          className="mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 focus:border-smf-500 focus:outline-none focus:ring focus:ring-smf-200"
        />
      </label>

      <div className="grid gap-3 sm:grid-cols-2">
        <label className="block text-sm">
          <span className="font-medium text-slate-700">Your name</span>
          <input value={name} onChange={(e) => setName(e.target.value)} maxLength={120}
            className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 focus:border-smf-500 focus:outline-none focus:ring focus:ring-smf-200" />
        </label>
        <label className="block text-sm">
          <span className="font-medium text-slate-700">Email <span className="text-xs text-slate-500">(optional)</span></span>
          <input value={email} onChange={(e) => setEmail(e.target.value)} type="email" maxLength={200}
            className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 focus:border-smf-500 focus:outline-none focus:ring focus:ring-smf-200" />
        </label>
      </div>

      {error && <div className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{error}</div>}

      <div className="flex justify-end">
        <button type="submit" disabled={busy}
          className="rounded-lg bg-smf-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-smf-700 disabled:opacity-50">
          {busy ? "Submitting…" : "Submit feedback"}
        </button>
      </div>
    </form>
  );
}
