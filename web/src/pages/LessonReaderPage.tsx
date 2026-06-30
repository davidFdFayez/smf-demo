import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import {
  completeLesson,
  getCourseBySlug,
  getEnrollment,
  getLesson,
  type CourseDetails,
  type EnrollmentProgress,
  type LessonDetails,
} from "../services/elearningApi";

export function LessonReaderPage() {
  const { slug = "", lessonId = "" } = useParams();
  const nav = useNavigate();
  const [course, setCourse] = useState<CourseDetails | null>(null);
  const [lesson, setLesson] = useState<LessonDetails | null>(null);
  const [progress, setProgress] = useState<EnrollmentProgress | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    const ctrl = new AbortController();
    getCourseBySlug(slug, ctrl.signal)
      .then(async (c) => {
        setCourse(c);
        const l = await getLesson(c.id, lessonId, ctrl.signal);
        setLesson(l);
        const memberId = localStorage.getItem("smf.memberId");
        if (memberId) {
          const p = await getEnrollment(c.id, memberId, ctrl.signal).catch(() => null);
          setProgress(p);
        }
      })
      .catch(() => setErr("Lesson not found."));
    return () => ctrl.abort();
  }, [slug, lessonId]);

  async function onComplete() {
    if (!progress || !lesson) return;
    setSaving(true);
    try {
      const p = await completeLesson(progress.enrollmentId, lesson.id);
      setProgress(p);
      const nextIdx = (course?.lessons.findIndex((l) => l.id === lesson.id) ?? -1) + 1;
      const next = course?.lessons[nextIdx];
      if (next) {
        nav(`/learn/${encodeURIComponent(slug)}/lessons/${next.id}`);
      } else {
        nav(`/learn/${encodeURIComponent(slug)}`);
      }
    } catch {
      setErr("Unable to mark this lesson complete. Try again.");
    } finally {
      setSaving(false);
    }
  }

  if (err) {
    return (
      <section className="mx-auto max-w-3xl px-6 py-12">
        <div className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-800">
          {err}
        </div>
      </section>
    );
  }
  if (!course || !lesson) {
    return (
      <section className="mx-auto max-w-3xl px-6 py-12">
        <p className="text-sm text-slate-500">Loading lesson…</p>
      </section>
    );
  }

  return (
    <section className="mx-auto max-w-3xl px-6 py-12">
      <Link
        to={`/learn/${encodeURIComponent(course.slug)}`}
        className="text-sm text-slate-500 hover:text-slate-800"
      >
        ← {course.title}
      </Link>
      <div className="mt-3 text-xs font-semibold uppercase tracking-wide text-smf-700">
        Lesson {lesson.order + 1} · {lesson.estimatedMinutes} min
      </div>
      <h1 className="mt-2 text-3xl font-bold tracking-tight text-slate-900">{lesson.title}</h1>
      <p className="mt-2 text-slate-600">{lesson.summary}</p>

      {lesson.videoUrl && (
        <div className="mt-6 aspect-video w-full overflow-hidden rounded-xl bg-black shadow-card">
          <iframe
            className="h-full w-full"
            src={lesson.videoUrl}
            allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
            allowFullScreen
            title={lesson.title}
          />
        </div>
      )}

      <article className="prose prose-slate mt-8 max-w-none whitespace-pre-wrap">
        {lesson.content}
      </article>

      {progress ? (
        <div className="mt-10 flex flex-wrap items-center justify-between gap-4 border-t border-slate-200 pt-6">
          <div className="text-xs text-slate-600">
            {progress.lessonsCompleted} / {progress.lessonsTotal} lessons · {progress.progressPercent}%
          </div>
          <button
            type="button"
            onClick={onComplete}
            disabled={saving}
            className="btn-primary"
          >
            {saving ? "Saving…" : "Mark complete & continue"}
          </button>
        </div>
      ) : (
        <div className="mt-10 rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900">
          You need to enroll to save progress.{" "}
          <Link to={`/learn/${encodeURIComponent(course.slug)}`} className="underline">
            Enroll now
          </Link>
          .
        </div>
      )}
    </section>
  );
}
