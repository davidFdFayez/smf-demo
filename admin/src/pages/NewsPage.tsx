import { useCallback, useEffect, useState } from "react";
import clsx from "clsx";
import {
  archiveNewsArticle,
  createNewsArticle,
  getNewsBySlugAdmin,
  listAllNews,
  publishNewsArticle,
  updateNewsArticle,
  type NewsArticleSummary,
  type NewsCategory,
  type UpdateNewsInput,
} from "../services/newsApi";

const CATEGORIES: NewsCategory[] = ["Announcement", "Championship", "Education", "PressRelease", "General"];

interface DraftForm {
  title: string; summary: string; body: string;
  category: NewsCategory; authorDisplayName: string; coverImageUrl: string;
}
const defaultDraft: DraftForm = {
  title: "", summary: "", body: "", category: "Announcement",
  authorDisplayName: "Federation Staff", coverImageUrl: "",
};

export function NewsPage() {
  const [form, setForm] = useState<DraftForm>(defaultDraft);
  const [articles, setArticles] = useState<NewsArticleSummary[]>([]);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [filter, setFilter] = useState<"all" | "published" | "draft" | "archived">("all");
  const [showForm, setShowForm] = useState(false);

  const load = useCallback(async (signal?: AbortSignal) => {
    setLoading(true);
    try {
      setArticles(await listAllNews(signal));
      setError(null);
    } catch { setError("Failed to load news."); }
    finally { setLoading(false); }
  }, []);

  useEffect(() => {
    const ctrl = new AbortController();
    void load(ctrl.signal);
    return () => ctrl.abort();
  }, [load]);

  const update = <K extends keyof DraftForm>(k: K, v: DraftForm[K]) =>
    setForm((f) => ({ ...f, [k]: v }));

  const submit = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault(); setSubmitting(true); setError(null);
    try {
      await createNewsArticle({
        title: form.title, summary: form.summary, body: form.body,
        category: form.category, authorDisplayName: form.authorDisplayName,
        coverImageUrl: form.coverImageUrl.trim() || undefined,
      });
      setForm(defaultDraft); setShowForm(false); await load();
    } catch { setError("Could not create article."); }
    finally { setSubmitting(false); }
  };

  const filtered = articles.filter((a) =>
    filter === "all" ? true
      : filter === "published" ? a.isPublished && !a.isArchived
        : filter === "archived" ? a.isArchived
          : !a.isPublished && !a.isArchived,
  );

  return (
    <div className="space-y-5">
      <div className="flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h2 className="text-xl font-semibold text-slate-900">News & Announcements</h2>
          <p className="text-sm text-slate-500">{articles.length} article{articles.length === 1 ? "" : "s"} · {loading ? "refreshing…" : "live"}</p>
        </div>
        <div className="flex gap-2">
          <button onClick={() => setShowForm((v) => !v)} className="btn-primary">
            {showForm ? "Close editor" : "+ New article"}
          </button>
        </div>
      </div>

      {showForm && (
        <form onSubmit={submit} className="card space-y-4 p-5">
          <h3 className="text-sm font-semibold text-slate-900">Draft article</h3>
          <div className="grid gap-4 md:grid-cols-2">
            <label className="block text-sm md:col-span-2">
              <span className="admin-label">Title</span>
              <input required maxLength={200} value={form.title} onChange={(e) => update("title", e.target.value)} className="admin-input" />
            </label>
            <label className="block text-sm">
              <span className="admin-label">Category</span>
              <select value={form.category} onChange={(e) => update("category", e.target.value as NewsCategory)} className="admin-input">
                {CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
              </select>
            </label>
            <label className="block text-sm">
              <span className="admin-label">Author</span>
              <input required maxLength={120} value={form.authorDisplayName} onChange={(e) => update("authorDisplayName", e.target.value)} className="admin-input" />
            </label>
            <label className="block text-sm md:col-span-2">
              <span className="admin-label">Cover image URL (optional)</span>
              <input maxLength={500} value={form.coverImageUrl} onChange={(e) => update("coverImageUrl", e.target.value)} className="admin-input" />
            </label>
            <label className="block text-sm md:col-span-2">
              <span className="admin-label">Summary</span>
              <input required maxLength={500} value={form.summary} onChange={(e) => update("summary", e.target.value)} className="admin-input" />
            </label>
            <label className="block text-sm md:col-span-2">
              <span className="admin-label">Body</span>
              <textarea required rows={8} maxLength={20_000} value={form.body} onChange={(e) => update("body", e.target.value)} className="admin-input font-mono text-xs" />
            </label>
          </div>
          {error && <div className="rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-800">{error}</div>}
          <button type="submit" disabled={submitting} className="btn-primary">{submitting ? "Saving…" : "Save as draft"}</button>
        </form>
      )}

      <div className="card p-3">
        <div className="flex flex-wrap gap-2">
          {(["all", "published", "draft", "archived"] as const).map((f) => (
            <button
              key={f}
              onClick={() => setFilter(f)}
              className={clsx(
                "rounded-lg px-3 py-1.5 text-xs font-medium capitalize transition",
                filter === f ? "bg-brand-600 text-white" : "bg-slate-100 text-slate-700 hover:bg-slate-200",
              )}
            >
              {f}
            </button>
          ))}
        </div>
      </div>

      {filtered.length === 0 ? (
        <div className="card p-8 text-center text-sm text-slate-500">No articles match this filter.</div>
      ) : (
        <ul className="space-y-3">
          {filtered.map((a) => (
            <li key={a.id} className={clsx("card p-5", a.isArchived && "opacity-70")}>
              <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                <div className="min-w-0">
                  <div className="flex items-center gap-2">
                    <h3 className="truncate text-base font-semibold text-slate-900">{a.title}</h3>
                    <StatusBadge article={a} />
                  </div>
                  <p className="mt-1 text-xs text-slate-500">
                    {a.category} · {a.authorDisplayName} · slug <span className="font-mono">{a.slug}</span> ·{" "}
                    {a.publishedAtUtc ? `published ${new Date(a.publishedAtUtc).toLocaleString()}` : `draft ${new Date(a.createdAtUtc).toLocaleString()}`}
                  </p>
                  <p className="mt-2 text-sm text-slate-700">{a.summary}</p>
                </div>
                <div className="flex shrink-0 gap-2 sm:flex-col">
                  {!a.isPublished && !a.isArchived && (
                    <button onClick={async () => { await publishNewsArticle(a.id); await load(); }} className="btn-primary text-xs">Publish</button>
                  )}
                  {!a.isArchived && (
                    <button onClick={async () => { await archiveNewsArticle(a.id); await load(); }} className="btn-secondary text-xs">Archive</button>
                  )}
                </div>
              </div>
              <ArticleEditor article={a} onSaved={load} />
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function ArticleEditor({
  article,
  onSaved,
}: {
  article: NewsArticleSummary;
  onSaved: () => Promise<void>;
}) {
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [form, setForm] = useState<UpdateNewsInput | null>(null);
  const [busy, setBusy] = useState(false);
  const [msg, setMsg] = useState<string | null>(null);

  const start = async () => {
    setOpen(true);
    if (form) return;
    setLoading(true); setMsg(null);
    try {
      const full = await getNewsBySlugAdmin(article.slug);
      setForm({
        title: full.title,
        summary: full.summary,
        body: full.body,
        category: full.category,
        coverImageUrl: full.coverImageUrl ?? null,
      });
    } catch (e) {
      setMsg((e as Error).message);
    } finally {
      setLoading(false);
    }
  };

  const save = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form) return;
    setBusy(true); setMsg(null);
    try {
      await updateNewsArticle(article.id, form);
      setOpen(false); await onSaved();
    } catch (e) {
      setMsg((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="mt-3 rounded-lg border border-dashed border-slate-200 bg-slate-50/60 p-3">
      <div className="flex items-center justify-between">
        <span className="text-xs font-semibold uppercase tracking-wide text-slate-500">
          Edit content
        </span>
        <button
          type="button"
          onClick={() => (open ? setOpen(false) : void start())}
          className="btn-secondary text-xs"
        >
          {open ? "Close" : "Edit"}
        </button>
      </div>
      {open && loading && <div className="mt-2 text-xs text-slate-500">Loading…</div>}
      {open && form && (
        <form onSubmit={save} className="mt-3 grid gap-3">
          <label className="block text-sm">
            <span className="admin-label">Title</span>
            <input
              required
              value={form.title}
              onChange={(e) => setForm({ ...form, title: e.target.value })}
              className="admin-input text-sm"
            />
          </label>
          <div className="grid gap-3 sm:grid-cols-2">
            <label className="block text-sm">
              <span className="admin-label">Category</span>
              <select
                value={form.category}
                onChange={(e) => setForm({ ...form, category: e.target.value as NewsCategory })}
                className="admin-input text-sm"
              >
                {CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
              </select>
            </label>
            <label className="block text-sm">
              <span className="admin-label">Cover image URL</span>
              <input
                value={form.coverImageUrl ?? ""}
                onChange={(e) => setForm({ ...form, coverImageUrl: e.target.value })}
                className="admin-input text-sm"
              />
            </label>
          </div>
          <label className="block text-sm">
            <span className="admin-label">Summary</span>
            <input
              required
              value={form.summary}
              onChange={(e) => setForm({ ...form, summary: e.target.value })}
              className="admin-input text-sm"
            />
          </label>
          <label className="block text-sm">
            <span className="admin-label">Body</span>
            <textarea
              required
              rows={8}
              value={form.body}
              onChange={(e) => setForm({ ...form, body: e.target.value })}
              className="admin-input font-mono text-xs"
            />
          </label>
          <div className="flex items-center gap-3">
            <button type="submit" disabled={busy} className="btn-primary text-xs">
              {busy ? "Saving…" : "Save changes"}
            </button>
            {msg && <span className="text-xs text-rose-700">{msg}</span>}
          </div>
        </form>
      )}
    </div>
  );
}

function StatusBadge({ article }: { article: NewsArticleSummary }) {
  const { color, label } = article.isArchived
    ? { color: "bg-slate-100 text-slate-600 ring-slate-300", label: "Archived" }
    : article.isPublished
      ? { color: "bg-emerald-50 text-emerald-700 ring-emerald-500/30", label: "Published" }
      : { color: "bg-amber-50 text-amber-800 ring-amber-400/40", label: "Draft" };
  return <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${color}`}>{label}</span>;
}
