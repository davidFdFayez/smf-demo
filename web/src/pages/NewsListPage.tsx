import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import clsx from "clsx";
import { useI18n } from "../i18n";
import { listPublishedNews, type NewsArticleSummary, type NewsCategory } from "../services/newsApi";

const CATEGORIES: NewsCategory[] = [
  "Announcement", "Championship", "Education", "PressRelease", "General",
];

export function NewsListPage() {
  const { t, locale } = useI18n();
  const [articles, setArticles] = useState<NewsArticleSummary[]>([]);
  const [category, setCategory] = useState<NewsCategory | "all">("all");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const ctrl = new AbortController();
    setLoading(true);
    listPublishedNews(category === "all" ? undefined : category, 50, ctrl.signal)
      .then((list) => { setArticles(list); setError(null); })
      .catch(() => setError(t("common.error_generic")))
      .finally(() => setLoading(false));
    return () => ctrl.abort();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [category]);

  const formatDate = useMemo(
    () => new Intl.DateTimeFormat(locale === "ar" ? "ar-SA" : "en-US", { dateStyle: "medium" }),
    [locale],
  );

  return (
    <section className="mx-auto max-w-7xl px-6 py-12">
      <h1 className="text-3xl font-bold tracking-tight text-slate-900 sm:text-4xl">{t("news.title")}</h1>
      <p className="mt-2 max-w-3xl text-slate-600">{t("news.subtitle")}</p>

      <div className="mt-8 flex flex-wrap gap-2">
        <button
          onClick={() => setCategory("all")}
          className={clsx(
            "rounded-full px-3.5 py-1.5 text-xs font-medium transition",
            category === "all" ? "bg-smf-600 text-white" : "bg-slate-100 text-slate-700 hover:bg-slate-200",
          )}
        >
          {t("news.all_categories")}
        </button>
        {CATEGORIES.map((c) => (
          <button
            key={c}
            onClick={() => setCategory(c)}
            className={clsx(
              "rounded-full px-3.5 py-1.5 text-xs font-medium transition",
              category === c ? "bg-smf-600 text-white" : "bg-slate-100 text-slate-700 hover:bg-slate-200",
            )}
          >
            {c}
          </button>
        ))}
      </div>

      {error && (
        <div className="mt-6 rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-800">
          {error}
        </div>
      )}

      {loading && articles.length === 0 ? (
        <p className="mt-10 text-sm text-slate-500">{t("common.loading")}</p>
      ) : articles.length === 0 ? (
        <p className="mt-10 text-sm text-slate-500">{t("news.empty")}</p>
      ) : (
        <div className="mt-8 grid gap-5 md:grid-cols-2 lg:grid-cols-3">
          {articles.map((a) => (
            <Link
              key={a.id}
              to={`/news/${a.slug}`}
              className="group public-card overflow-hidden transition hover:-translate-y-0.5 hover:shadow-md"
            >
              {a.coverImageUrl ? (
                <img src={a.coverImageUrl} alt="" className="h-44 w-full object-cover" loading="lazy" />
              ) : (
                <div className="flex h-44 items-center justify-center bg-gradient-to-br from-smf-500 to-smf-700 text-white">
                  <span className="text-xs font-semibold uppercase tracking-wide">{a.category}</span>
                </div>
              )}
              <div className="p-5">
                <div className="text-[11px] font-semibold uppercase tracking-wide text-smf-700">
                  {a.category}
                </div>
                <h3 className="mt-1 line-clamp-2 text-base font-semibold text-slate-900 group-hover:text-smf-700">
                  {a.title}
                </h3>
                <p className="mt-2 line-clamp-3 text-sm text-slate-600">{a.summary}</p>
                <div className="mt-3 flex items-center justify-between text-xs text-slate-500">
                  <span>{a.authorDisplayName}</span>
                  <span>
                    {a.publishedAtUtc ? formatDate.format(new Date(a.publishedAtUtc)) : "—"}
                  </span>
                </div>
              </div>
            </Link>
          ))}
        </div>
      )}
    </section>
  );
}
