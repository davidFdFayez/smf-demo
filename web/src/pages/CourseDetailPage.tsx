import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import {
  getCourseBySlug,
  enrollInCourse,
  getEnrollment,
  type CourseDetails,
  type EnrollmentProgress,
} from "../services/elearningApi";

export function CourseDetailPage() {
  const { slug = "" } = useParams();
  const [course, setCourse] = useState<CourseDetails | null>(null);
  const [progress, setProgress] = useState<EnrollmentProgress | null>(null);
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState<string | null>(null);
  const [memberId, setMemberId] = useState(
    () => localStorage.getItem("smf.memberId") ?? "",
  );
  const [enrolling, setEnrolling] = useState(false);

  useEffect(() => {
    const ctrl = new AbortController();
    setLoading(true);
    getCourseBySlug(slug, ctrl.signal)
      .then(async (c) => {
        setCourse(c);
        setErr(null);
        if (memberId) {
          const prog = await getEnrollment(c.id, memberId, ctrl.signal).catch(() => null);
          setProgress(prog);
        }
      })
      .catch(() => setErr("Course not found or no longer available."))
      .finally(() => setLoading(false));
    return () => ctrl.abort();
  }, [slug]);

  async function onEnroll() {
    if (!course || !memberId) return;
    setEnrolling(true);
    try {
      localStorage.setItem("smf.memberId", memberId);
      const p = await enrollInCourse(course.id, memberId);
      setProgress(p);
    } catch {
      setErr("Unable to enroll — check the member ID and try again.");
    } finally {
      setEnrolling(false);
    }
  }

  if (loading) return <LoadingShell />;
  if (err || !course) return <ErrorShell message={err ?? "Course not found."} />;

  return (
    <section className="mx-auto max-w-4xl px-6 py-12">
      <Link to="/learn" className="text-sm text-slate-500 hover:text-slate-800">
        ← Back to courses
      </Link>

      <header className="mt-3">
        <div className="inline-flex flex-wrap items-center gap-2 text-[11px] font-semibold uppercase tracking-wide text-smf-700">
          <span>{course.category.replace(/([A-Z])/g, " $1").trim()}</span>
          <span className="text-slate-300">•</span>
          <span>{course.level}</span>
          <span className="text-slate-300">•</span>
          <span>{course.lessons.length} lessons</span>
          <span className="text-slate-300">•</span>
          <span>{course.totalMinutes} min total</span>
        </div>
        <h1 className="mt-3 text-3xl font-bold tracking-tight text-slate-900 sm:text-4xl">
          {course.title}
        </h1>
        <p className="mt-2 text-slate-600">{course.summary}</p>
        <p className="mt-2 text-xs text-slate-500">Taught by {course.instructorName}</p>
      </header>

      <div className="mt-8 public-card p-5">
        <h2 className="text-sm font-semibold text-slate-900">Your progress</h2>
        {!progress ? (
          <>
            <p className="mt-2 text-sm text-slate-600">
              Enter your federation member ID to enroll and track progress.
            </p>
            <div className="mt-3 flex flex-col gap-3 sm:flex-row">
              <input
                className="field-input flex-1"
                placeholder="Member GUID"
                value={memberId}
                onChange={(e) => setMemberId(e.target.value)}
              />
              <button
                type="button"
                disabled={!memberId || enrolling}
                onClick={onEnroll}
                className="btn-primary"
              >
                {enrolling ? "Enrolling…" : "Enroll"}
              </button>
            </div>
          </>
        ) : (
          <ProgressBar p={progress} />
        )}
      </div>

      <h2 className="mt-8 text-sm font-semibold uppercase tracking-wide text-slate-500">
        Curriculum
      </h2>
      <ol className="mt-3 space-y-3">
        {course.lessons.map((l, i) => {
          const done = progress?.lessonsCompleted ?? 0;
          const completed = i < done;
          return (
            <li
              key={l.id}
              className="public-card flex items-start justify-between gap-4 p-4"
            >
              <div className="min-w-0">
                <div className="flex items-center gap-2 text-xs text-slate-500">
                  <span className="inline-flex h-6 w-6 items-center justify-center rounded-full bg-slate-100 text-[11px] font-semibold text-slate-700">
                    {l.order + 1}
                  </span>
                  <span>{l.estimatedMinutes} min</span>
                  {l.hasVideo && <span className="chip bg-smf-50 text-smf-700 ring-smf-500/30">Video</span>}
                </div>
                <h3 className="mt-1 text-base font-semibold text-slate-900">{l.title}</h3>
                <p className="text-sm text-slate-600">{l.summary}</p>
              </div>
              <div className="shrink-0">
                {progress ? (
                  <Link
                    to={`/learn/${encodeURIComponent(course.slug)}/lessons/${l.id}`}
                    className={completed ? "btn-outline" : "btn-primary"}
                  >
                    {completed ? "Review" : "Start"}
                  </Link>
                ) : (
                  <span className="chip bg-slate-100 text-slate-600 ring-slate-300/40">
                    Enroll to unlock
                  </span>
                )}
              </div>
            </li>
          );
        })}
      </ol>
    </section>
  );
}

function ProgressBar({ p }: { p: EnrollmentProgress }) {
  return (
    <div className="mt-3">
      <div className="flex items-center justify-between text-xs font-medium text-slate-600">
        <span>
          {p.lessonsCompleted} / {p.lessonsTotal} lessons
        </span>
        <span>{p.progressPercent}%</span>
      </div>
      <div className="mt-2 h-2.5 overflow-hidden rounded-full bg-slate-100">
        <div
          className="h-full rounded-full bg-gradient-to-r from-smf-500 to-smf-700 transition-all"
          style={{ width: `${Math.min(100, p.progressPercent)}%` }}
        />
      </div>
      {p.status === "Completed" && (
        <p className="mt-3 text-sm font-medium text-smf-700">
          Course completed — a certificate will be issued shortly.
        </p>
      )}
    </div>
  );
}

function LoadingShell() {
  return (
    <section className="mx-auto max-w-4xl px-6 py-12">
      <p className="text-sm text-slate-500">Loading course…</p>
    </section>
  );
}
function ErrorShell({ message }: { message: string }) {
  return (
    <section className="mx-auto max-w-4xl px-6 py-12">
      <div className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-800">
        {message}
      </div>
      <Link to="/learn" className="btn-primary mt-6 inline-flex">
        Back to courses
      </Link>
    </section>
  );
}
