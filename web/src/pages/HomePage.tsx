import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useI18n } from "../i18n";
import { listPublishedNews, type NewsArticleSummary } from "../services/newsApi";
import { listEvents, type EventSummary } from "../services/eventsApi";
import { getAdminStats, type AdminStats } from "../services/adminApi";
import { SocialFeed } from "../features/social/SocialFeed";
import { FeedbackList } from "../features/feedback/FeedbackList";
import { FeedbackForm } from "../features/feedback/FeedbackForm";

export function HomePage() {
  const { t, locale } = useI18n();
  const [news, setNews] = useState<NewsArticleSummary[]>([]);
  const [events, setEvents] = useState<EventSummary[]>([]);
  const [stats, setStats] = useState<AdminStats | null>(null);

  useEffect(() => {
    const ctrl = new AbortController();
    listPublishedNews(undefined, 3, ctrl.signal).then(setNews).catch(() => void 0);
    listEvents(ctrl.signal).then((all) => {
      const visible = all
        .filter((e) => e.status !== "Cancelled" && e.status !== "Draft")
        .sort((a, b) => new Date(a.startsAtUtc).getTime() - new Date(b.startsAtUtc).getTime())
        .slice(0, 3);
      setEvents(visible);
    }).catch(() => void 0);
    getAdminStats(ctrl.signal).then(setStats).catch(() => void 0);
    return () => ctrl.abort();
  }, []);

  const nf = new Intl.NumberFormat(locale === "ar" ? "ar-SA" : "en-US");
  const df = new Intl.DateTimeFormat(locale === "ar" ? "ar-SA" : "en-US", {
    dateStyle: "medium", timeStyle: "short",
  });

  return (
    <>
      {/* Hero */}
      <section className="relative overflow-hidden bg-gradient-to-br from-navy-800 via-navy-700 to-smf-700 text-white">
        <div
          className="absolute inset-0 opacity-40"
          style={{ backgroundImage: "radial-gradient(circle at 1px 1px, rgba(255,255,255,0.08) 1px, transparent 0)", backgroundSize: "22px 22px" }}
          aria-hidden
        />
        <div className="relative mx-auto grid max-w-7xl gap-10 px-6 py-16 md:grid-cols-2 md:py-24 md:gap-12">
          <div>
            <div className="inline-flex items-center gap-2 rounded-full bg-white/10 px-3 py-1 text-xs font-semibold uppercase tracking-wide text-smf-200 ring-1 ring-white/10">
              {t("home.eyebrow")}
            </div>
            <h1 className="mt-5 text-4xl font-bold leading-tight tracking-tight sm:text-5xl md:text-6xl">
              {t("home.headline_1")}
              <br />
              <span className="bg-gradient-to-r from-smf-200 to-white bg-clip-text text-transparent">
                {t("home.headline_2")}
              </span>
            </h1>
            <p className="mt-5 max-w-xl text-base text-slate-200 sm:text-lg">
              {t("home.subheadline")}
            </p>
            <div className="mt-8 flex flex-wrap items-center gap-3">
              <Link to="/register" className="rounded-lg bg-white px-5 py-3 text-sm font-semibold text-smf-800 shadow-lg shadow-smf-900/30 transition hover:bg-slate-50">
                {t("home.cta.primary")}
              </Link>
              <Link to="/watch" className="rounded-lg border border-white/30 bg-white/5 px-5 py-3 text-sm font-semibold text-white transition hover:bg-white/10">
                {t("home.cta.secondary")}
              </Link>
            </div>
          </div>
          <div className="relative">
            <div className="absolute -inset-1 rounded-3xl bg-white/5 blur-2xl" aria-hidden />
            <div className="relative grid grid-cols-2 gap-4">
              <HeroStat label={t("home.kpi.members")} value={stats ? nf.format(stats.totalMembers) : "—"} accent="emerald" />
              <HeroStat label={t("home.kpi.clubs")} value={stats ? nf.format(stats.totalClubs) : "—"} accent="sky" />
              <HeroStat label={t("home.kpi.events")} value={stats ? nf.format(stats.totalEvents) : "—"} accent="violet" />
              <HeroStat label="Upcoming" value={stats ? nf.format(stats.upcomingEvents) : "—"} accent="amber" />
            </div>
          </div>
        </div>
      </section>

      {/* Pillars */}
      <section className="mx-auto max-w-7xl px-6 py-16">
        <h2 className="text-center text-2xl font-bold tracking-tight text-slate-900 sm:text-3xl">
          {t("home.section.pillars")}
        </h2>
        <div className="mt-10 grid gap-5 md:grid-cols-3">
          <Pillar
            title={t("home.pillar.athlete.title")}
            desc={t("home.pillar.athlete.desc")}
            icon={<IconAthlete />}
            cta={<Link to="/register" className="text-sm font-semibold text-smf-700 hover:text-smf-800">{t("home.cta.primary")} →</Link>}
          />
          <Pillar
            title={t("home.pillar.club.title")}
            desc={t("home.pillar.club.desc")}
            icon={<IconFlag />}
            cta={<Link to="/clubs" className="text-sm font-semibold text-smf-700 hover:text-smf-800">{t("nav.clubs")} →</Link>}
          />
          <Pillar
            title={t("home.pillar.referee.title")}
            desc={t("home.pillar.referee.desc")}
            icon={<IconBolt />}
            cta={<Link to="/watch" className="text-sm font-semibold text-smf-700 hover:text-smf-800">{t("nav.scoreboard")} →</Link>}
          />
        </div>
      </section>

      {/* News */}
      <section className="bg-slate-50">
        <div className="mx-auto max-w-7xl px-6 py-16">
          <div className="flex items-end justify-between">
            <h2 className="text-2xl font-bold tracking-tight text-slate-900 sm:text-3xl">
              {t("home.section.news")}
            </h2>
            <Link to="/news" className="text-sm font-semibold text-smf-700 hover:text-smf-800">
              {t("home.section.news.cta")}
            </Link>
          </div>

          {news.length === 0 ? (
            <p className="mt-8 text-sm text-slate-500">{t("news.empty")}</p>
          ) : (
            <div className="mt-8 grid gap-5 md:grid-cols-3">
              {news.map((a) => (
                <Link
                  key={a.id}
                  to={`/news/${a.slug}`}
                  className="group public-card overflow-hidden transition hover:-translate-y-0.5 hover:shadow-md"
                >
                  {a.coverImageUrl ? (
                    <img src={a.coverImageUrl} alt="" className="h-40 w-full object-cover" loading="lazy" />
                  ) : (
                    <div className="flex h-40 items-center justify-center bg-gradient-to-br from-smf-500 to-smf-700 text-white">
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
                    <div className="mt-3 text-xs text-slate-500">
                      {a.publishedAtUtc ? new Date(a.publishedAtUtc).toLocaleDateString(locale === "ar" ? "ar-SA" : "en-US") : "—"}
                    </div>
                  </div>
                </Link>
              ))}
            </div>
          )}
        </div>
      </section>

      {/* Events */}
      <section className="mx-auto max-w-7xl px-6 py-16">
        <div className="flex items-end justify-between">
          <h2 className="text-2xl font-bold tracking-tight text-slate-900 sm:text-3xl">
            {t("home.section.events")}
          </h2>
          <Link to="/events" className="text-sm font-semibold text-smf-700 hover:text-smf-800">
            {t("home.section.events.cta")}
          </Link>
        </div>
        {events.length === 0 ? (
          <p className="mt-8 text-sm text-slate-500">{t("events.empty")}</p>
        ) : (
          <ul className="mt-8 space-y-3">
            {events.map((e) => (
              <li key={e.id} className="public-card flex flex-col gap-3 p-5 sm:flex-row sm:items-center sm:justify-between">
                <div>
                  <h3 className="text-base font-semibold text-slate-900">{e.title}</h3>
                  <div className="mt-0.5 text-xs text-slate-500">{e.location}</div>
                  <div className="mt-1 text-sm text-slate-600">
                    {df.format(new Date(e.startsAtUtc))}
                  </div>
                </div>
                <div className="text-xs text-slate-500">
                  {(e.entryFeeMinor / 100).toFixed(2)} {e.currency}
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className="mx-auto mt-16 max-w-7xl px-4 sm:px-6">
        <SocialFeed />
      </section>

      <section className="mx-auto mt-16 max-w-7xl px-4 pb-16 sm:px-6">
        <div className="grid gap-8 lg:grid-cols-2">
          <div className="space-y-4">
            <header>
              <h2 className="text-2xl font-bold text-slate-900">What members are saying</h2>
              <p className="mt-1 text-sm text-slate-500">
                Honest reviews from athletes, coaches, and clubs across the federation.
              </p>
            </header>
            <FeedbackList minRating={4} limit={4} />
          </div>
          <FeedbackForm
            title="Share your experience"
            description="Tell us how the federation is doing. New reviews go live after a quick moderation check."
          />
        </div>
      </section>
    </>
  );
}

function HeroStat({ label, value, accent }: { label: string; value: string; accent: "emerald" | "sky" | "violet" | "amber" }) {
  const map = {
    emerald: "from-emerald-400/20 to-emerald-500/10 border-emerald-200/20",
    sky: "from-sky-400/20 to-sky-500/10 border-sky-200/20",
    violet: "from-violet-400/20 to-violet-500/10 border-violet-200/20",
    amber: "from-amber-400/20 to-amber-500/10 border-amber-200/20",
  } as const;
  return (
    <div className={`rounded-2xl border bg-gradient-to-br ${map[accent]} p-5 backdrop-blur`}>
      <div className="text-xs font-semibold uppercase tracking-wide text-white/70">{label}</div>
      <div className="mt-1 text-3xl font-bold tabular-nums">{value}</div>
    </div>
  );
}

function Pillar({ title, desc, icon, cta }: { title: string; desc: string; icon: React.ReactNode; cta: React.ReactNode }) {
  return (
    <div className="public-card p-6 transition hover:-translate-y-0.5 hover:shadow-md">
      <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-smf-50 text-smf-700">
        {icon}
      </div>
      <h3 className="mt-4 text-lg font-semibold text-slate-900">{title}</h3>
      <p className="mt-1 text-sm text-slate-600">{desc}</p>
      <div className="mt-4">{cta}</div>
    </div>
  );
}

function IconAthlete() { return <svg width="22" height="22" viewBox="0 0 24 24" fill="none"><circle cx="12" cy="6" r="3" stroke="currentColor" strokeWidth="1.6"/><path d="M5 21c0-3.9 3.1-7 7-7s7 3.1 7 7" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round"/></svg>; }
function IconFlag() { return <svg width="22" height="22" viewBox="0 0 24 24" fill="none"><path d="M5 3v18M5 5h10l-2 3 2 3H5" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round"/></svg>; }
function IconBolt() { return <svg width="22" height="22" viewBox="0 0 24 24" fill="none"><path d="M13 2L4 14h7l-1 8 9-12h-7l1-8z" stroke="currentColor" strokeWidth="1.6" strokeLinejoin="round"/></svg>; }
