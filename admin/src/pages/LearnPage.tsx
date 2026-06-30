import { useEffect, useMemo, useState } from "react";
import clsx from "clsx";
import {
  addLesson,
  archiveCourse,
  createCourse,
  getCourseBySlug,
  listAllCourses,
  publishCourse,
  type AddLessonInput,
  type CourseCategory,
  type CourseDetails,
  type CourseLevel,
  type CourseSummary,
  type CreateCourseInput,
} from "../services/elearningApi";

const CATEGORIES: CourseCategory[] = [
  "AthleteFundamentals",
  "CoachingCertification",
  "RefereeEducation",
  "SafeguardingAndIntegrity",
  "AntiDoping",
  "StrengthAndConditioning",
  "General",
];

const LEVELS: CourseLevel[] = ["Beginner", "Intermediate", "Advanced", "Certification"];

export function LearnPage() {
  const [courses, setCourses] = useState<CourseSummary[]>([]);
  const [selectedSlug, setSelectedSlug] = useState<string | null>(null);
  const [detail, setDetail] = useState<CourseDetails | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = async () => {
    try { setCourses(await listAllCourses()); setError(null); }
    catch (e) { setError((e as Error).message); }
  };
  useEffect(() => { void load(); }, []);

  useEffect(() => {
    if (!selectedSlug) { setDetail(null); return; }
    const ctrl = new AbortController();
    getCourseBySlug(selectedSlug, ctrl.signal).then(setDetail).catch(() => setDetail(null));
    return () => ctrl.abort();
  }, [selectedSlug]);

  return (
    <div className="space-y-5">
      <div className="flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h2 className="text-xl font-semibold text-slate-900">E-learning</h2>
          <p className="text-sm text-slate-500">{courses.length} courses · {courses.filter((c) => c.isPublished).length} published</p>
        </div>
        <button className="btn-primary" onClick={() => setShowForm((v) => !v)}>
          {showForm ? "Close form" : "+ New course"}
        </button>
      </div>

      {error && <div className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-800">{error}</div>}

      {showForm && (
        <CreateCourseForm
          onCancel={() => setShowForm(false)}
          onCreated={async (c) => {
            setShowForm(false);
            setSelectedSlug(c.slug);
            await load();
          }}
        />
      )}

      <div className="grid gap-5 lg:grid-cols-[1fr_1.2fr]">
        <div className="card divide-y divide-slate-100">
          {courses.length === 0 ? (
            <div className="p-6 text-sm text-slate-500">No courses yet.</div>
          ) : (
            courses.map((c) => (
              <button
                key={c.id}
                type="button"
                onClick={() => setSelectedSlug(c.slug)}
                className={clsx(
                  "block w-full px-4 py-3 text-left transition hover:bg-slate-50",
                  selectedSlug === c.slug && "bg-brand-50",
                )}
              >
                <div className="flex items-center justify-between gap-3">
                  <div className="min-w-0">
                    <div className="truncate text-sm font-semibold text-slate-900">{c.title}</div>
                    <div className="mt-0.5 truncate text-xs text-slate-500">
                      {c.category} · {c.level} · {c.lessonCount} lessons · {c.totalMinutes} min
                    </div>
                  </div>
                  <CourseStatusPill c={c} />
                </div>
              </button>
            ))
          )}
        </div>

        <div>
          {detail ? (
            <CourseDetailEditor detail={detail} onChanged={async () => {
              const refreshed = await getCourseBySlug(detail.slug).catch(() => null);
              setDetail(refreshed);
              await load();
            }} />
          ) : (
            <div className="card p-8 text-sm text-slate-500">
              Select a course from the list to manage lessons, publishing, and archival.
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

function CreateCourseForm({
  onCancel,
  onCreated,
}: {
  onCancel: () => void;
  onCreated: (c: CourseDetails) => Promise<void>;
}) {
  const [form, setForm] = useState<CreateCourseInput>({
    title: "", summary: "", category: "General", level: "Beginner",
    instructorName: "", coverImageUrl: "",
  });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const up = <K extends keyof CreateCourseInput>(k: K) =>
    (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>) => {
      setForm((f) => ({ ...f, [k]: e.target.value as CreateCourseInput[K] }));
    };

  const submit = async (e: React.FormEvent) => {
    e.preventDefault(); setBusy(true); setError(null);
    try {
      const created = await createCourse({
        ...form,
        coverImageUrl: form.coverImageUrl?.trim() ? form.coverImageUrl : undefined,
      });
      await onCreated(created);
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  };

  return (
    <form className="card space-y-4 p-5" onSubmit={submit}>
      <div className="grid gap-4 md:grid-cols-2">
        <label className="block text-sm">
          <span className="admin-label">Title *</span>
          <input required className="admin-input mt-1" value={form.title} onChange={up("title")} />
        </label>
        <label className="block text-sm">
          <span className="admin-label">Instructor *</span>
          <input required className="admin-input mt-1" value={form.instructorName} onChange={up("instructorName")} />
        </label>
        <label className="block text-sm md:col-span-2">
          <span className="admin-label">Summary *</span>
          <textarea required className="admin-input mt-1 min-h-[70px]" value={form.summary} onChange={up("summary")} />
        </label>
        <label className="block text-sm">
          <span className="admin-label">Category</span>
          <select className="admin-input mt-1" value={form.category} onChange={up("category")}>
            {CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
          </select>
        </label>
        <label className="block text-sm">
          <span className="admin-label">Level</span>
          <select className="admin-input mt-1" value={form.level} onChange={up("level")}>
            {LEVELS.map((l) => <option key={l} value={l}>{l}</option>)}
          </select>
        </label>
        <label className="block text-sm md:col-span-2">
          <span className="admin-label">Cover image URL</span>
          <input className="admin-input mt-1" value={form.coverImageUrl ?? ""} onChange={up("coverImageUrl")} />
        </label>
      </div>
      {error && <div className="rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-800">{error}</div>}
      <div className="flex gap-2">
        <button type="submit" disabled={busy} className="btn-primary">{busy ? "Creating…" : "Create course"}</button>
        <button type="button" className="btn-secondary" onClick={onCancel}>Cancel</button>
      </div>
    </form>
  );
}

function CourseDetailEditor({
  detail,
  onChanged,
}: {
  detail: CourseDetails;
  onChanged: () => Promise<void>;
}) {
  const status = useMemo(() => {
    if (detail.isArchived) return "Archived";
    if (detail.isPublished) return "Published";
    return "Draft";
  }, [detail]);

  return (
    <div className="space-y-4">
      <div className="card p-5">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <div className="text-xs uppercase tracking-wide text-slate-500">{detail.category} · {detail.level}</div>
            <h3 className="mt-0.5 truncate text-lg font-semibold text-slate-900">{detail.title}</h3>
            <div className="text-xs text-slate-500">by {detail.instructorName}</div>
            <p className="mt-2 text-sm text-slate-700">{detail.summary}</p>
          </div>
          <span className={clsx(
            "chip",
            status === "Published" && "bg-emerald-50 text-emerald-700 ring-emerald-500/30",
            status === "Draft" && "bg-slate-100 text-slate-700 ring-slate-500/20",
            status === "Archived" && "bg-rose-50 text-rose-700 ring-rose-500/30",
          )}>
            {status}
          </span>
        </div>
        <div className="mt-4 flex flex-wrap gap-2">
          {!detail.isPublished && !detail.isArchived && (
            <button
              className="btn-primary text-xs"
              onClick={async () => { await publishCourse(detail.id); await onChanged(); }}
            >
              Publish course
            </button>
          )}
          {!detail.isArchived && (
            <button
              className="btn-secondary text-xs"
              onClick={async () => { await archiveCourse(detail.id); await onChanged(); }}
            >
              Archive
            </button>
          )}
        </div>
      </div>

      <div className="card p-5">
        <h4 className="text-sm font-semibold text-slate-900">Curriculum</h4>
        {detail.lessons.length === 0 ? (
          <p className="mt-2 text-sm text-slate-500">No lessons yet. Add one below.</p>
        ) : (
          <ol className="mt-3 space-y-2">
            {detail.lessons.map((l) => (
              <li key={l.id} className="rounded-xl border border-slate-100 p-3">
                <div className="flex items-center justify-between gap-3">
                  <div className="min-w-0">
                    <div className="truncate text-sm font-semibold text-slate-900">
                      {l.order}. {l.title}
                    </div>
                    <div className="text-xs text-slate-500">
                      {l.estimatedMinutes} min{l.hasVideo ? " · video" : ""}
                    </div>
                  </div>
                </div>
                {l.summary && <p className="mt-1 text-xs text-slate-600">{l.summary}</p>}
              </li>
            ))}
          </ol>
        )}

        <AddLessonForm courseId={detail.id} onAdded={onChanged} />
      </div>
    </div>
  );
}

function AddLessonForm({
  courseId,
  onAdded,
}: {
  courseId: string;
  onAdded: () => Promise<void>;
}) {
  const [form, setForm] = useState<AddLessonInput>({
    title: "", summary: "", content: "", videoUrl: "", estimatedMinutes: 10,
  });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault(); setBusy(true); setError(null);
    try {
      await addLesson(courseId, {
        ...form,
        videoUrl: form.videoUrl?.trim() ? form.videoUrl : undefined,
      });
      setForm({ title: "", summary: "", content: "", videoUrl: "", estimatedMinutes: 10 });
      await onAdded();
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  };

  return (
    <form className="mt-5 rounded-xl border border-dashed border-slate-200 p-4" onSubmit={submit}>
      <h5 className="text-xs font-semibold uppercase tracking-wide text-slate-500">
        Add lesson
      </h5>
      <div className="mt-2 grid gap-3 md:grid-cols-2">
        <input className="admin-input text-sm" placeholder="Lesson title" required
          value={form.title} onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))} />
        <input className="admin-input text-sm" placeholder="Estimated minutes" type="number" min={1}
          value={form.estimatedMinutes}
          onChange={(e) => setForm((f) => ({ ...f, estimatedMinutes: Number(e.target.value) || 1 }))} />
        <input className="admin-input text-sm md:col-span-2" placeholder="Short summary"
          value={form.summary} onChange={(e) => setForm((f) => ({ ...f, summary: e.target.value }))} />
        <input className="admin-input text-sm md:col-span-2" placeholder="Video URL (optional)"
          value={form.videoUrl ?? ""}
          onChange={(e) => setForm((f) => ({ ...f, videoUrl: e.target.value }))} />
        <textarea className="admin-input min-h-[80px] text-sm md:col-span-2"
          placeholder="Lesson body (markdown/plain text)" required
          value={form.content}
          onChange={(e) => setForm((f) => ({ ...f, content: e.target.value }))} />
      </div>
      {error && <div className="mt-2 rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-800">{error}</div>}
      <div className="mt-3">
        <button type="submit" disabled={busy} className="btn-primary text-xs">
          {busy ? "Adding…" : "Add lesson"}
        </button>
      </div>
    </form>
  );
}

function CourseStatusPill({ c }: { c: CourseSummary }) {
  if (c.isArchived) return <span className="chip bg-rose-50 text-rose-700 ring-rose-500/30">Archived</span>;
  if (c.isPublished) return <span className="chip bg-emerald-50 text-emerald-700 ring-emerald-500/30">Live</span>;
  return <span className="chip bg-slate-100 text-slate-700 ring-slate-500/20">Draft</span>;
}
