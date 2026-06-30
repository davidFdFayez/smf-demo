import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import {
  listPublishedCourses,
  type CourseCategory,
  type CourseLevel,
  type CourseSummary,
} from "../services/elearningApi";

const CATEGORIES: { value: CourseCategory; label: string }[] = [
  { value: "AthleteFundamentals", label: "Athlete fundamentals" },
  { value: "CoachingCertification", label: "Coaching certification" },
  { value: "RefereeEducation", label: "Referee education" },
  { value: "SafeguardingAndIntegrity", label: "Safeguarding & integrity" },
  { value: "AntiDoping", label: "Anti-doping" },
  { value: "StrengthAndConditioning", label: "Strength & conditioning" },
  { value: "General", label: "General" },
];

const LEVELS: CourseLevel[] = ["Beginner", "Intermediate", "Advanced", "Certification"];

export function CoursesListPage() {
  const [courses, setCourses] = useState<CourseSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState<string | null>(null);
  const [category, setCategory] = useState<CourseCategory | "">("");
  const [level, setLevel] = useState<CourseLevel | "">("");

  useEffect(() => {
    const ctrl = new AbortController();
    setLoading(true);
    listPublishedCourses(category || undefined, level || undefined, ctrl.signal)
      .then((list) => {
        setCourses(list);
        setErr(null);
      })
      .catch(() => setErr("Unable to load courses right now."))
      .finally(() => setLoading(false));
    return () => ctrl.abort();
  }, [category, level]);

  const grouped = useMemo(() => {
    const map = new Map<CourseCategory, CourseSummary[]>();
    for (const c of courses) {
      const bucket = map.get(c.category) ?? [];
      bucket.push(c);
      map.set(c.category, bucket);
    }
    return map;
  }, [courses]);

  return (
    <section className="mx-auto max-w-7xl px-6 py-12">
      <header>
        <div className="inline-flex rounded-full bg-smf-50 px-3 py-1 text-xs font-semibold uppercase tracking-wide text-smf-700">
          E-learning platform
        </div>
        <h1 className="mt-3 text-3xl font-bold tracking-tight text-slate-900 sm:text-4xl">
          Federation learning library
        </h1>
        <p className="mt-2 max-w-3xl text-slate-600">
          Self-paced courses for athletes, coaches, and referees. Complete all lessons to earn
          a federation-issued digital certificate.
        </p>
      </header>

      <div className="mt-6 flex flex-wrap gap-3">
        <select
          className="field-input w-64"
          value={category}
          onChange={(e) => setCategory(e.target.value as CourseCategory | "")}
        >
          <option value="">All categories</option>
          {CATEGORIES.map((c) => (
            <option key={c.value} value={c.value}>
              {c.label}
            </option>
          ))}
        </select>
        <select
          className="field-input w-48"
          value={level}
          onChange={(e) => setLevel(e.target.value as CourseLevel | "")}
        >
          <option value="">All levels</option>
          {LEVELS.map((l) => (
            <option key={l} value={l}>
              {l}
            </option>
          ))}
        </select>
      </div>

      {err && (
        <div className="mt-6 rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-800">
          {err}
        </div>
      )}

      {loading && courses.length === 0 ? (
        <p className="mt-10 text-sm text-slate-500">Loading courses…</p>
      ) : courses.length === 0 ? (
        <p className="mt-10 text-sm text-slate-500">
          No published courses match your filters yet.
        </p>
      ) : (
        <div className="mt-8 space-y-10">
          {Array.from(grouped.entries()).map(([cat, list]) => (
            <div key={cat}>
              <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500">
                {CATEGORIES.find((c) => c.value === cat)?.label ?? cat}
              </h2>
              <ul className="mt-3 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
                {list.map((c) => (
                  <li key={c.id}>
                    <Link
                      to={`/learn/${encodeURIComponent(c.slug)}`}
                      className="public-card block overflow-hidden p-5 transition hover:-translate-y-0.5 hover:shadow-md"
                    >
                      <div className="flex items-center gap-2 text-[11px] font-semibold uppercase tracking-wide text-smf-700">
                        <span>{c.level}</span>
                        <span className="text-slate-300">•</span>
                        <span>{c.lessonCount} lessons</span>
                        <span className="text-slate-300">•</span>
                        <span>{c.totalMinutes} min</span>
                      </div>
                      <h3 className="mt-2 text-lg font-semibold text-slate-900">{c.title}</h3>
                      <p className="mt-1 text-sm text-slate-600 line-clamp-3">{c.summary}</p>
                      <div className="mt-4 flex items-center justify-between border-t border-slate-100 pt-3 text-xs text-slate-500">
                        <span>Instructor: {c.instructorName}</span>
                        <span>{c.enrollmentCount} enrolled</span>
                      </div>
                    </Link>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}
