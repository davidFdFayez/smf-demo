import { useState } from "react";
import { NavLink, Outlet, useLocation } from "react-router-dom";
import clsx from "clsx";

interface NavItem {
  to: string;
  label: string;
  desc: string;
  icon: JSX.Element;
}

const NAV: NavItem[] = [
  { to: "/",               label: "Dashboard",    desc: "Federation-wide KPIs",                icon: <IconGrid /> },
  { to: "/members",        label: "Members",      desc: "Athletes, coaches, referees, clubs",  icon: <IconUsers /> },
  { to: "/clubs",          label: "Clubs",        desc: "Directory + approvals",               icon: <IconFlag /> },
  { to: "/events",         label: "Events",       desc: "Tournaments and camps",               icon: <IconCalendar /> },
  { to: "/brackets",       label: "Brackets",     desc: "Live bracket engine",                 icon: <IconTrophy /> },
  { to: "/news",           label: "News",         desc: "CMS — draft, publish, archive",       icon: <IconMegaphone /> },
  { to: "/safeguarding",   label: "Safeguarding", desc: "Reports + triage",                    icon: <IconShield /> },
  { to: "/referee",        label: "Scoring",      desc: "Live referee console",                icon: <IconBolt /> },
  { to: "/head-referee",   label: "Head ref",     desc: "Overrides + live strike feed",        icon: <IconGavel /> },
  { to: "/timekeeper",     label: "Timekeeper",   desc: "Authoritative round clock",           icon: <IconClock /> },
  { to: "/certificates",   label: "Certificates", desc: "Issue, verify, download",             icon: <IconAward /> },
  { to: "/learn",          label: "Learn",        desc: "Courses & lessons CMS",               icon: <IconBook /> },
  { to: "/tenants",        label: "Tenants",      desc: "White-label scoring brands",          icon: <IconTag /> },
  { to: "/analytics",      label: "Analytics",    desc: "Federation pulse & insights",         icon: <IconChart /> },
  { to: "/store/products", label: "Store SKUs",   desc: "Catalog, pricing, stock",             icon: <IconBox /> },
  { to: "/store/orders",   label: "Store orders", desc: "E-commerce orders + invoices",        icon: <IconReceipt /> },
  { to: "/broadcasts",     label: "Broadcasts",   desc: "Email · SMS · push campaigns",        icon: <IconBroadcast /> },
  { to: "/feedback",       label: "Feedback",     desc: "User reviews, moderation queue",      icon: <IconChat /> },
  { to: "/social-highlights", label: "Social",    desc: "Curated social media feed",           icon: <IconHeart /> },
  { to: "/governance",     label: "Governance",   desc: "SOPC transparency documents",         icon: <IconDoc /> },
  { to: "/policies",       label: "Policies",     desc: "Terms · Privacy · Code of Conduct",   icon: <IconScale /> },
  { to: "/consents",       label: "Consents",     desc: "Parental consent + audit log",        icon: <IconLock /> },
  { to: "/exports",        label: "Exports",      desc: "CSV reports",                         icon: <IconDownload /> },
];

export function AdminLayout() {
  const [open, setOpen] = useState(false);
  const { pathname } = useLocation();
  const active =
    NAV.find((n) => (n.to === "/" ? pathname === "/" : pathname.startsWith(n.to))) ?? NAV[0];

  return (
    <div className="min-h-screen bg-slate-100 text-slate-800">
      {/* Sidebar (desktop: fixed; mobile: drawer) */}
      <aside
        className={clsx(
          "fixed inset-y-0 left-0 z-40 flex w-72 shrink-0 flex-col border-r border-slate-800/40 bg-ink-950 text-slate-200 transition-transform",
          "lg:translate-x-0",
          open ? "translate-x-0" : "-translate-x-full",
        )}
      >
        <div className="flex h-16 shrink-0 items-center gap-3 border-b border-white/5 px-6">
          <LogoMark />
          <div>
            <div className="text-sm font-semibold text-white">SMF Admin</div>
            <div className="text-[11px] uppercase tracking-wide text-slate-400">
              Operator console
            </div>
          </div>
        </div>

        <nav className="flex-1 overflow-y-auto px-3 py-4 [scrollbar-width:thin] [scrollbar-color:theme(colors.slate.700)_transparent]">
          <ul className="space-y-1">
            {NAV.map((n) => (
              <li key={n.to}>
                <NavLink
                  to={n.to}
                  end={n.to === "/"}
                  onClick={() => setOpen(false)}
                  className={({ isActive }) =>
                    clsx(
                      "group flex items-start gap-3 rounded-lg px-3 py-2.5 text-sm transition",
                      isActive
                        ? "bg-brand-600/90 text-white shadow-sm"
                        : "text-slate-300 hover:bg-white/5 hover:text-white",
                    )
                  }
                >
                  <span className="mt-0.5 text-slate-400 group-hover:text-white">
                    {n.icon}
                  </span>
                  <span>
                    <span className="block font-medium">{n.label}</span>
                    <span className="block text-[11px] text-slate-400 group-hover:text-slate-200">
                      {n.desc}
                    </span>
                  </span>
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>

        <div className="m-4 mt-2 shrink-0 rounded-lg border border-white/5 bg-white/5 p-3 text-xs text-slate-400">
          <div className="font-medium text-slate-200">Signed in as</div>
          <div className="mt-0.5">Federation Staff</div>
          <div className="mt-2 text-[11px] text-slate-500">
            Connected to <span className="font-mono text-slate-300">/api</span>
          </div>
        </div>
      </aside>

      {/* Mobile scrim */}
      {open && (
        <button
          type="button"
          className="fixed inset-0 z-30 bg-black/30 backdrop-blur-sm lg:hidden"
          onClick={() => setOpen(false)}
          aria-label="Close navigation"
        />
      )}

      {/* Main */}
      <div className="lg:pl-72">
        <header className="sticky top-0 z-20 border-b border-slate-200 bg-white/80 backdrop-blur">
          <div className="flex h-16 items-center justify-between gap-3 px-4 sm:px-6">
            <div className="flex items-center gap-3">
              <button
                type="button"
                onClick={() => setOpen((v) => !v)}
                className="rounded-lg border border-slate-300 bg-white p-2 text-slate-700 hover:bg-slate-50 lg:hidden"
                aria-label="Open navigation"
              >
                <IconMenu />
              </button>
              <div>
                <div className="text-xs uppercase tracking-wide text-slate-400">
                  Admin
                </div>
                <h1 className="text-base font-semibold text-slate-900">{active.label}</h1>
              </div>
            </div>
            <div className="flex items-center gap-2 text-xs text-slate-500">
              <span className="hidden sm:inline">API:</span>
              <span className="rounded-md border border-emerald-200 bg-emerald-50 px-2 py-0.5 font-mono text-emerald-700">
                http://localhost:5080
              </span>
            </div>
          </div>
        </header>

        <main className="min-h-[calc(100vh-4rem)] p-4 sm:p-6 lg:p-8">
          <Outlet />
        </main>
      </div>
    </div>
  );
}

function LogoMark() {
  return (
    <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-gradient-to-br from-brand-500 to-brand-700 text-white shadow-md">
      <svg viewBox="0 0 24 24" fill="none" className="h-5 w-5" aria-hidden>
        <path d="M4 5l8-2 8 2v6c0 5-3.5 8-8 10-4.5-2-8-5-8-10V5z"
          stroke="currentColor" strokeWidth="1.6" strokeLinejoin="round" />
        <path d="M9 12l2 2 4-4" stroke="currentColor" strokeWidth="1.6"
          strokeLinecap="round" strokeLinejoin="round" />
      </svg>
    </div>
  );
}

function IconMenu() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" aria-hidden>
      <path d="M4 6h16M4 12h16M4 18h16" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
    </svg>
  );
}
function IconGrid() { return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><rect x="3" y="3" width="7" height="7" stroke="currentColor" strokeWidth="1.5"/><rect x="14" y="3" width="7" height="7" stroke="currentColor" strokeWidth="1.5"/><rect x="3" y="14" width="7" height="7" stroke="currentColor" strokeWidth="1.5"/><rect x="14" y="14" width="7" height="7" stroke="currentColor" strokeWidth="1.5"/></svg>; }
function IconUsers() { return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><circle cx="9" cy="8" r="3.5" stroke="currentColor" strokeWidth="1.5"/><path d="M3 20c.8-3 3-5 6-5s5.2 2 6 5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/><circle cx="17" cy="9" r="2.5" stroke="currentColor" strokeWidth="1.5"/><path d="M14.5 16.2c1-.7 2.2-1.2 3.5-1.2 2.2 0 3.7 1.3 4.2 3" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/></svg>; }
function IconFlag() { return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><path d="M5 3v18M5 5h10l-2 3 2 3H5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/></svg>; }
function IconCalendar() { return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><rect x="3" y="5" width="18" height="16" rx="2" stroke="currentColor" strokeWidth="1.5"/><path d="M3 10h18M8 3v4M16 3v4" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/></svg>; }
function IconMegaphone() { return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><path d="M3 10v4l10 4V6L3 10zM13 9l7-3v12l-7-3" stroke="currentColor" strokeWidth="1.5" strokeLinejoin="round"/></svg>; }
function IconShield() { return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><path d="M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6l8-3z" stroke="currentColor" strokeWidth="1.5" strokeLinejoin="round"/></svg>; }
function IconBolt() { return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><path d="M13 2L4 14h7l-1 8 9-12h-7l1-8z" stroke="currentColor" strokeWidth="1.5" strokeLinejoin="round"/></svg>; }
function IconDownload() { return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><path d="M12 3v12m0 0l-4-4m4 4l4-4M4 21h16" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/></svg>; }
function IconBook() { return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><path d="M4 5a2 2 0 012-2h13v16H6a2 2 0 00-2 2V5zM4 19a2 2 0 012-2h13" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/></svg>; }
function IconTag() { return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><path d="M3 12V5a2 2 0 012-2h7l9 9-7 7-9-9z" stroke="currentColor" strokeWidth="1.5" strokeLinejoin="round"/><circle cx="8" cy="8" r="1.5" fill="currentColor"/></svg>; }
function IconChart() { return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><path d="M4 20V8m6 12V4m6 16v-8m6 8V12" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/></svg>; }
function IconTrophy() { return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><path d="M8 4h8v5a4 4 0 11-8 0V4zM5 5h3m11 0h3m-9 10v3m-3 3h6" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/></svg>; }
function IconGavel() { return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><path d="M3 20h10M10 5l4 4M6 9l6-6 5 5-6 6-5-5zM13 12l6 6" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/></svg>; }
function IconClock() { return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="1.5"/><path d="M12 7v5l3 2" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/></svg>; }
function IconAward() { return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><circle cx="12" cy="9" r="5" stroke="currentColor" strokeWidth="1.5"/><path d="M8 14l-2 7 6-3 6 3-2-7" stroke="currentColor" strokeWidth="1.5" strokeLinejoin="round"/></svg>; }
function IconBox() { return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><path d="M3 7l9-4 9 4-9 4-9-4zM3 7v10l9 4 9-4V7M12 11v10" stroke="currentColor" strokeWidth="1.5" strokeLinejoin="round"/></svg>; }
function IconReceipt() { return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><path d="M5 3h14v18l-3-2-2 2-2-2-2 2-2-2-3 2V3zM8 8h8M8 12h8M8 16h5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/></svg>; }
function IconBroadcast() { return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><path d="M5 12a7 7 0 0114 0M2 12a10 10 0 0120 0M9 12a3 3 0 116 0v8h-6v-8z" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/></svg>; }
function IconChat() { return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><path d="M4 5h16v11H8l-4 4V5z" stroke="currentColor" strokeWidth="1.5" strokeLinejoin="round"/><path d="M8 10h8M8 13h6" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/></svg>; }
function IconHeart() { return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><path d="M12 21s-7-4.5-9.5-9.5C1 7.6 4 4 7.5 4c2 0 3.5 1 4.5 2.5C13 5 14.5 4 16.5 4 20 4 23 7.6 21.5 11.5 19 16.5 12 21 12 21z" stroke="currentColor" strokeWidth="1.5" strokeLinejoin="round"/></svg>; }
function IconDoc() { return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><path d="M6 3h8l4 4v14H6V3z" stroke="currentColor" strokeWidth="1.5" strokeLinejoin="round"/><path d="M14 3v4h4M9 13h6M9 17h6" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/></svg>; }
function IconScale() { return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><path d="M12 3v18M3 8h18M6 8l-3 7c0 2 1.5 3 3 3s3-1 3-3l-3-7zm12 0l-3 7c0 2 1.5 3 3 3s3-1 3-3l-3-7z" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/></svg>; }
function IconLock() { return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><rect x="5" y="11" width="14" height="10" rx="2" stroke="currentColor" strokeWidth="1.5"/><path d="M8 11V8a4 4 0 018 0v3" stroke="currentColor" strokeWidth="1.5"/></svg>; }
