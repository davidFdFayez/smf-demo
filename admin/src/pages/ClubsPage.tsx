import { useEffect, useState } from "react";
import clsx from "clsx";
import { approveClub, listClubs, type ClubSummary } from "../services/clubsApi";
import { downloadCsv } from "../services/exportApi";
import {
  getMicrositeBySlug,
  updateMicrosite,
  type ClubMicrositeDetails,
  type UpdateMicrositeInput,
} from "../services/micrositesApi";

export function ClubsPage() {
  const [clubs, setClubs] = useState<ClubSummary[]>([]);
  const [filter, setFilter] = useState<"all" | "Pending" | "Active" | "Suspended">("all");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = async () => {
    setLoading(true);
    try {
      const s = filter === "all" ? undefined : filter;
      setClubs(await listClubs(s));
      setError(null);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { void load(); /* eslint-disable-next-line */ }, [filter]);

  const approve = async (id: string) => {
    try { await approveClub(id); await load(); }
    catch (e) { setError((e as Error).message); }
  };

  return (
    <div className="space-y-5">
      <div className="flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h2 className="text-xl font-semibold text-slate-900">Clubs</h2>
          <p className="text-sm text-slate-500">{clubs.length} shown</p>
        </div>
        <div className="flex gap-2">
          <button onClick={() => void downloadCsv("clubs")} className="btn-secondary">Download CSV</button>
          <button onClick={() => void load()} disabled={loading} className="btn-secondary">
            {loading ? "Refreshing…" : "Refresh"}
          </button>
        </div>
      </div>

      <div className="card p-3">
        <div className="flex flex-wrap gap-2">
          {(["all", "Pending", "Active", "Suspended"] as const).map((s) => (
            <button
              key={s}
              onClick={() => setFilter(s)}
              className={clsx(
                "rounded-lg px-3 py-1.5 text-xs font-medium transition",
                filter === s ? "bg-brand-600 text-white" : "bg-slate-100 text-slate-700 hover:bg-slate-200",
              )}
            >
              {s === "all" ? "All" : s}
            </button>
          ))}
        </div>
      </div>

      {error && <div className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-800">{error}</div>}

      {clubs.length === 0 ? (
        <div className="card p-8 text-center text-sm text-slate-500">No clubs match this filter.</div>
      ) : (
        <ul className="grid gap-4 md:grid-cols-2">
          {clubs.map((c) => (
            <li key={c.id} className="card p-5">
              <div className="flex items-start justify-between gap-4">
                <div className="min-w-0">
                  <div className="flex items-baseline gap-2">
                    <h3 className="truncate text-base font-semibold text-slate-900">{c.name}</h3>
                    <span className="font-mono text-xs text-slate-400">/{c.slug}</span>
                  </div>
                  <div className="mt-0.5 text-xs text-slate-500">{c.city}</div>
                  {c.description && <p className="mt-2 text-sm text-slate-700">{c.description}</p>}
                  <div className="mt-3 text-xs text-slate-600">
                    {c.contactEmail} · {c.contactPhone}
                  </div>
                  {c.websiteUrl && (
                    <a className="mt-1 block text-xs text-brand-700 hover:text-brand-800" href={c.websiteUrl} target="_blank" rel="noreferrer">
                      {c.websiteUrl}
                    </a>
                  )}
                </div>
                <div className="flex shrink-0 flex-col items-end gap-2">
                  <StatusPill status={c.status} />
                  <span className="text-xs text-slate-500">{c.memberCount} members</span>
                  {c.status === "Pending" && (
                    <button onClick={() => void approve(c.id)} className="btn-primary text-xs">Approve</button>
                  )}
                </div>
              </div>
              <MicrositeEditor club={c} />
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function MicrositeEditor({ club }: { club: ClubSummary }) {
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [data, setData] = useState<ClubMicrositeDetails | null>(null);
  const [form, setForm] = useState<UpdateMicrositeInput>({});
  const [busy, setBusy] = useState(false);
  const [msg, setMsg] = useState<string | null>(null);

  const toggle = async () => {
    if (open) { setOpen(false); return; }
    setOpen(true); setLoading(true); setMsg(null);
    try {
      const details = await getMicrositeBySlug(club.slug);
      setData(details);
      setForm({
        headline: details.micrositeHeadline ?? "",
        about: details.micrositeAbout ?? "",
        heroImageUrl: details.micrositeHeroImageUrl ?? "",
        primaryColor: details.micrositePrimaryColor ?? "",
        instagramHandle: details.micrositeInstagramHandle ?? "",
        twitterHandle: details.micrositeTwitterHandle ?? "",
        youtubeChannel: details.micrositeYoutubeChannel ?? "",
      });
    } catch (e) {
      setMsg((e as Error).message);
    } finally { setLoading(false); }
  };

  const save = async (e: React.FormEvent) => {
    e.preventDefault(); setBusy(true); setMsg(null);
    try {
      const sanitised: UpdateMicrositeInput = Object.fromEntries(
        Object.entries(form).map(([k, v]) => [k, typeof v === "string" && v.trim() === "" ? undefined : v]),
      );
      const updated = await updateMicrosite(club.id, sanitised);
      setData(updated);
      setMsg("Saved");
    } catch (e) { setMsg((e as Error).message); }
    finally { setBusy(false); }
  };

  return (
    <div className="mt-4 rounded-xl border border-dashed border-slate-200 p-3">
      <div className="flex items-center justify-between gap-2">
        <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">
          Microsite
        </div>
        <div className="flex items-center gap-2">
          <a className="text-xs text-brand-700 hover:text-brand-800"
            href={`/clubs/${encodeURIComponent(club.slug)}`} target="_blank" rel="noreferrer">
            Preview
          </a>
          <button type="button" className="btn-secondary text-xs" onClick={toggle}>
            {open ? "Close" : "Edit"}
          </button>
        </div>
      </div>

      {open && (
        <div className="mt-3">
          {loading ? (
            <p className="text-xs text-slate-500">Loading…</p>
          ) : data ? (
            <form className="space-y-3" onSubmit={save}>
              <label className="block text-sm">
                <span className="admin-label">Headline</span>
                <input className="admin-input mt-1 text-sm"
                  value={form.headline ?? ""}
                  onChange={(e) => setForm({ ...form, headline: e.target.value })} />
              </label>
              <label className="block text-sm">
                <span className="admin-label">About</span>
                <textarea className="admin-input mt-1 min-h-[70px] text-sm"
                  value={form.about ?? ""}
                  onChange={(e) => setForm({ ...form, about: e.target.value })} />
              </label>
              <div className="grid gap-3 sm:grid-cols-2">
                <label className="block text-sm">
                  <span className="admin-label">Hero image URL</span>
                  <input className="admin-input mt-1 text-sm"
                    value={form.heroImageUrl ?? ""}
                    onChange={(e) => setForm({ ...form, heroImageUrl: e.target.value })} />
                </label>
                <label className="block text-sm">
                  <span className="admin-label">Primary color</span>
                  <input className="admin-input mt-1 text-sm font-mono"
                    placeholder="#0f766e"
                    pattern="^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$"
                    value={form.primaryColor ?? ""}
                    onChange={(e) => setForm({ ...form, primaryColor: e.target.value })} />
                </label>
                <label className="block text-sm">
                  <span className="admin-label">Instagram</span>
                  <input className="admin-input mt-1 text-sm"
                    placeholder="@handle"
                    value={form.instagramHandle ?? ""}
                    onChange={(e) => setForm({ ...form, instagramHandle: e.target.value })} />
                </label>
                <label className="block text-sm">
                  <span className="admin-label">Twitter / X</span>
                  <input className="admin-input mt-1 text-sm"
                    placeholder="@handle"
                    value={form.twitterHandle ?? ""}
                    onChange={(e) => setForm({ ...form, twitterHandle: e.target.value })} />
                </label>
                <label className="block text-sm sm:col-span-2">
                  <span className="admin-label">YouTube channel URL</span>
                  <input className="admin-input mt-1 text-sm"
                    value={form.youtubeChannel ?? ""}
                    onChange={(e) => setForm({ ...form, youtubeChannel: e.target.value })} />
                </label>
              </div>
              <div className="flex items-center gap-2">
                <button type="submit" disabled={busy} className="btn-primary text-xs">
                  {busy ? "Saving…" : "Save microsite"}
                </button>
                {msg && <span className="text-xs text-slate-500">{msg}</span>}
              </div>
            </form>
          ) : (
            msg && <p className="text-xs text-rose-700">{msg}</p>
          )}
        </div>
      )}
    </div>
  );
}

function StatusPill({ status }: { status: ClubSummary["status"] }) {
  const cls =
    status === "Active" ? "bg-emerald-50 text-emerald-700 ring-emerald-500/30"
      : status === "Suspended" ? "bg-rose-50 text-rose-700 ring-rose-500/30"
        : "bg-amber-50 text-amber-800 ring-amber-400/40";
  return <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${cls}`}>{status}</span>;
}
