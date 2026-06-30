import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { getAdminStats, type AdminStats } from "../services/adminApi";

export function DashboardPage() {
  const [stats, setStats] = useState<AdminStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const ctrl = new AbortController();
    setLoading(true);
    getAdminStats(ctrl.signal)
      .then((s) => { setStats(s); setError(null); })
      .catch((e) => setError((e as Error).message))
      .finally(() => setLoading(false));
    return () => ctrl.abort();
  }, []);

  const fmt = new Intl.NumberFormat("en-US");
  const sar = (minor: number) =>
    new Intl.NumberFormat("en-US", {
      style: "currency",
      currency: "SAR",
      minimumFractionDigits: 2,
    }).format(minor / 100);

  if (loading && !stats) {
    return <div className="card p-6 text-sm text-slate-500">Loading dashboard…</div>;
  }
  if (error) {
    return (
      <div className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-800">
        {error}
      </div>
    );
  }
  if (!stats) return null;

  return (
    <div className="space-y-6">
      {/* Hero KPIs */}
      <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
        <Kpi label="Total members" value={fmt.format(stats.totalMembers)} trend="All registered identities" />
        <Kpi label="Pending approvals" value={fmt.format(stats.pendingMemberApprovals)} accent="amber" trend="Awaiting admin review" />
        <Kpi label="Clubs registered" value={fmt.format(stats.totalClubs)} trend={`${stats.pendingClubApprovals} pending`} />
        <Kpi label="Upcoming events" value={fmt.format(stats.upcomingEvents)} trend={`${stats.totalEvents} total`} />
      </div>

      {/* Revenue strip */}
      <div className="grid gap-4 md:grid-cols-2">
        <RevenueCard label="Membership revenue · this month" value={sar(stats.membershipRevenueThisMonthMinor)} tint="emerald" />
        <RevenueCard label="Event revenue · this month" value={sar(stats.eventRevenueThisMonthMinor)} tint="brand" />
      </div>

      {/* Distributions */}
      <div className="grid gap-6 lg:grid-cols-2">
        <Distribution title="Members by role" data={stats.membersByRole} />
        <Distribution title="Members by status" data={stats.membersByStatus} />
      </div>

      {/* Recent registrations */}
      <div className="card overflow-hidden">
        <div className="flex items-center justify-between border-b border-slate-200 px-5 py-3">
          <h3 className="text-sm font-semibold text-slate-900">Recent registrations</h3>
          <Link to="/members" className="text-xs font-medium text-brand-700 hover:text-brand-800">
            View all members →
          </Link>
        </div>
        <table className="min-w-full divide-y divide-slate-200 text-sm">
          <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
            <tr>
              <th className="px-5 py-2.5">SMF ID</th>
              <th className="px-5 py-2.5">Name</th>
              <th className="px-5 py-2.5">Role</th>
              <th className="px-5 py-2.5">Status</th>
              <th className="px-5 py-2.5">Registered</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {stats.recentRegistrations.length === 0 && (
              <tr><td className="px-5 py-6 text-slate-400" colSpan={5}>No recent registrations.</td></tr>
            )}
            {stats.recentRegistrations.map((r) => (
              <tr key={r.id} className="hover:bg-slate-50/70">
                <td className="px-5 py-2.5 font-mono text-xs text-slate-700">{r.smF_ID}</td>
                <td className="px-5 py-2.5">
                  <Link to={`/members/${r.id}`} className="text-slate-900 hover:text-brand-700">
                    {r.fullName}
                  </Link>
                </td>
                <td className="px-5 py-2.5 text-slate-700">{r.role}</td>
                <td className="px-5 py-2.5">
                  <StatusPill status={r.status} />
                </td>
                <td className="px-5 py-2.5 text-slate-500">
                  {new Date(r.createdAtUtc).toLocaleDateString()}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function Kpi({ label, value, trend, accent }: { label: string; value: string; trend?: string; accent?: "amber" }) {
  const base = "card p-5";
  const accentCls = accent === "amber" ? "border-amber-300 bg-amber-50" : "";
  return (
    <div className={`${base} ${accentCls}`}>
      <div className="text-[11px] font-semibold uppercase tracking-wide text-slate-500">{label}</div>
      <div className="mt-1 text-3xl font-bold text-slate-900">{value}</div>
      {trend && <div className="mt-2 text-xs text-slate-500">{trend}</div>}
    </div>
  );
}

function RevenueCard({ label, value, tint }: { label: string; value: string; tint: "emerald" | "brand" }) {
  const gradient =
    tint === "emerald"
      ? "from-emerald-500 to-emerald-700"
      : "from-brand-500 to-brand-700";
  return (
    <div className={`overflow-hidden rounded-xl bg-gradient-to-br ${gradient} p-5 text-white shadow-card`}>
      <div className="text-[11px] font-semibold uppercase tracking-wide text-white/70">
        {label}
      </div>
      <div className="mt-1 text-3xl font-bold tracking-tight">{value}</div>
    </div>
  );
}

function Distribution({ title, data }: { title: string; data: Record<string, number> }) {
  const entries = Object.entries(data);
  const total = entries.reduce((a, [, v]) => a + v, 0);
  return (
    <div className="card p-5">
      <h3 className="mb-3 text-sm font-semibold text-slate-900">{title}</h3>
      {entries.length === 0 ? (
        <div className="text-sm text-slate-400">—</div>
      ) : (
        <ul className="space-y-3">
          {entries.map(([k, v]) => {
            const pct = total === 0 ? 0 : Math.round((v / total) * 100);
            return (
              <li key={k}>
                <div className="mb-1 flex justify-between text-xs">
                  <span className="font-medium text-slate-700">{k}</span>
                  <span className="text-slate-500">{v} · {pct}%</span>
                </div>
                <div className="h-2 overflow-hidden rounded-full bg-slate-100">
                  <div className="h-full bg-brand-500" style={{ width: `${pct}%` }} />
                </div>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}

function StatusPill({ status }: { status: string }) {
  const cls =
    status === "Approved" || status === "Active"
      ? "bg-emerald-50 text-emerald-700 ring-emerald-500/30"
      : "bg-amber-50 text-amber-800 ring-amber-400/40";
  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${cls}`}>
      {status}
    </span>
  );
}
