import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useI18n } from "../i18n";
import { getNewsBySlug, type NewsArticleDetails } from "../services/newsApi";

export function NewsDetailPage() {
  const { t, locale } = useI18n();
  const { slug = "" } = useParams();
  const [article, setArticle] = useState<NewsArticleDetails | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const ctrl = new AbortController();
    setLoading(true);
    getNewsBySlug(slug, ctrl.signal)
      .then((a) => { setArticle(a); setError(null); })
      .catch(() => setError(t("common.error_generic")))
      .finally(() => setLoading(false));
    return () => ctrl.abort();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [slug]);

  const df = new Intl.DateTimeFormat(locale === "ar" ? "ar-SA" : "en-US", {
    dateStyle: "long",
  });

  return (
    <article className="mx-auto max-w-3xl px-6 py-12">
      <Link to="/news" className="text-sm font-semibold text-smf-700 hover:text-smf-800">
        {t("news.back")}
      </Link>

      {loading && <p className="mt-8 text-sm text-slate-500">{t("common.loading")}</p>}

      {error && (
        <div className="mt-8 rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-800">
          {error}
        </div>
      )}

      {article && (
        <>
          <div className="mt-6 text-[11px] font-semibold uppercase tracking-wide text-smf-700">
            {article.category}
          </div>
          <h1 className="mt-2 text-3xl font-bold tracking-tight text-slate-900 sm:text-4xl">
            {article.title}
          </h1>
          <div className="mt-3 flex items-center gap-3 text-sm text-slate-500">
            <span>{article.authorDisplayName}</span>
            <span>·</span>
            <span>{article.publishedAtUtc ? df.format(new Date(article.publishedAtUtc)) : "—"}</span>
          </div>

          {article.coverImageUrl && (
            <img
              src={article.coverImageUrl}
              alt=""
              className="mt-8 w-full rounded-2xl object-cover shadow-card"
            />
          )}

          <p className="mt-8 text-lg text-slate-700">{article.summary}</p>

          <div className="prose prose-slate mt-6 max-w-none whitespace-pre-wrap text-base leading-relaxed text-slate-700">
            {article.body}
          </div>
        </>
      )}
    </article>
  );
}
