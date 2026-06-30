import { useState } from "react";
import { Link, NavLink, Outlet } from "react-router-dom";
import clsx from "clsx";
import { LanguageSwitcher, useI18n } from "../i18n";
import { FloatingChatbot } from "../features/chatbot/FloatingChatbot";

const NAV = [
  { to: "/",            key: "nav.home"         as const },
  { to: "/news",        key: "nav.news"         as const },
  { to: "/rankings",    key: "nav.rankings"     as const },
  { to: "/events",      key: "nav.events"       as const },
  { to: "/clubs",       key: "nav.clubs"        as const },
  { to: "/learn",       key: "nav.learn"        as const },
  { to: "/store",       key: "nav.store"        as const },
  { to: "/watch",       key: "nav.scoreboard"   as const },
  { to: "/safeguarding", key: "nav.safeguarding" as const },
];

export function PublicLayout() {
  const { t, dir } = useI18n();
  const [open, setOpen] = useState(false);

  return (
    <div className="flex min-h-screen flex-col bg-white" dir={dir}>
      {/* Top bar */}
      <header className="sticky top-0 z-30 border-b border-slate-200/70 bg-white/85 backdrop-blur">
        <div className="mx-auto flex max-w-7xl items-center justify-between px-4 py-3 sm:px-6">
          <Link to="/" className="flex items-center gap-3">
            <LogoMark />
            <span className="flex flex-col">
              <span className="text-sm font-semibold text-slate-900">{t("app.title")}</span>
              <span className="text-[11px] uppercase tracking-wide text-smf-700">{t("app.brand")}</span>
            </span>
          </Link>

          <nav className="hidden items-center gap-1 lg:flex">
            {NAV.map((n) => (
              <NavLink
                key={n.to}
                to={n.to}
                end={n.to === "/"}
                className={({ isActive }) =>
                  clsx(
                    "rounded-lg px-3 py-2 text-sm font-medium transition",
                    isActive
                      ? "bg-smf-50 text-smf-700"
                      : "text-slate-600 hover:bg-slate-50 hover:text-slate-900",
                  )
                }
              >
                {t(n.key)}
              </NavLink>
            ))}
          </nav>

          <div className="hidden items-center gap-2 lg:flex">
            <LanguageSwitcher />
            <Link to="/register" className="btn-primary">{t("nav.register")}</Link>
          </div>

          <button
            type="button"
            className="rounded-lg border border-slate-300 bg-white p-2 text-slate-700 hover:bg-slate-50 lg:hidden"
            onClick={() => setOpen((v) => !v)}
            aria-label="Toggle navigation"
          >
            <svg viewBox="0 0 24 24" width="20" height="20" fill="none">
              <path d="M4 6h16M4 12h16M4 18h16" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
            </svg>
          </button>
        </div>

        {open && (
          <div className="border-t border-slate-200 bg-white lg:hidden">
            <div className="mx-auto flex max-w-7xl flex-col gap-1 px-4 py-3">
              {NAV.map((n) => (
                <NavLink
                  key={n.to}
                  to={n.to}
                  end={n.to === "/"}
                  onClick={() => setOpen(false)}
                  className={({ isActive }) =>
                    clsx(
                      "rounded-lg px-3 py-2 text-sm font-medium",
                      isActive ? "bg-smf-50 text-smf-700" : "text-slate-700 hover:bg-slate-50",
                    )
                  }
                >
                  {t(n.key)}
                </NavLink>
              ))}
              <div className="mt-2 flex items-center gap-2">
                <LanguageSwitcher />
                <Link to="/register" onClick={() => setOpen(false)} className="btn-primary flex-1">
                  {t("nav.register")}
                </Link>
              </div>
            </div>
          </div>
        )}
      </header>

      <main className="flex-1">
        <Outlet />
      </main>

      <footer className="mt-16 border-t border-slate-200 bg-slate-50">
        <div className="mx-auto grid max-w-7xl gap-8 px-6 py-10 md:grid-cols-3">
          <div>
            <div className="flex items-center gap-2">
              <LogoMark />
              <span className="text-sm font-semibold text-slate-900">{t("app.title")}</span>
            </div>
            <p className="mt-3 text-sm text-slate-600">
              {t("footer.tagline")}
            </p>
          </div>
          <div>
            <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">
              {t("footer.explore")}
            </div>
            <ul className="mt-3 space-y-1.5 text-sm">
              <li><Link className="hover:text-smf-700" to="/register">{t("nav.register")}</Link></li>
              <li><Link className="hover:text-smf-700" to="/events">{t("nav.events")}</Link></li>
              <li><Link className="hover:text-smf-700" to="/rankings">{t("nav.rankings")}</Link></li>
              <li><Link className="hover:text-smf-700" to="/clubs">{t("nav.clubs")}</Link></li>
            </ul>
          </div>
          <div>
            <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">
              {t("footer.integrity")}
            </div>
            <ul className="mt-3 space-y-1.5 text-sm">
              <li><Link className="hover:text-smf-700" to="/safeguarding">{t("nav.safeguarding")}</Link></li>
              <li><Link className="hover:text-smf-700" to="/governance">Governance &amp; transparency</Link></li>
              <li><Link className="hover:text-smf-700" to="/about">{t("nav.about")}</Link></li>
            </ul>
            <div className="mt-4 text-xs text-slate-500">
              © {new Date().getFullYear()} {t("app.title")}.
            </div>
          </div>
        </div>
      </footer>

      <FloatingChatbot />
    </div>
  );
}

function LogoMark() {
  return (
    <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-gradient-to-br from-smf-500 to-smf-700 text-white shadow-md">
      <svg viewBox="0 0 24 24" fill="none" className="h-5 w-5" aria-hidden>
        <path d="M4 5l8-2 8 2v6c0 5-3.5 8-8 10-4.5-2-8-5-8-10V5z"
          stroke="currentColor" strokeWidth="1.6" strokeLinejoin="round" />
        <path d="M9 12l2 2 4-4" stroke="currentColor" strokeWidth="1.6"
          strokeLinecap="round" strokeLinejoin="round" />
      </svg>
    </span>
  );
}
