import { useCallback, useEffect, useMemo, useState } from "react";
import {
  cancelBroadcast,
  createBroadcast,
  listBroadcasts,
  previewBroadcastAudience,
  sendBroadcast,
  type BroadcastChannel,
  type BroadcastStatus,
  type BroadcastSummary,
  type CreateBroadcastInput,
  type MemberRole,
} from "../services/communicationApi";
import type { PagedResult } from "../services/storeApi";

/**
 * Multi-channel broadcast console. Lets the operator:
 *
 *  * Compose a campaign (subject + body + channel selection + audience filter).
 *  * Preview audience size in real time before sending.
 *  * Trigger a synchronous send and watch the resulting counters.
 *  * Cancel drafts, replay completed campaigns from the list.
 */

const STATUSES: BroadcastStatus[] = ["Draft", "Scheduled", "Sending", "Sent", "Failed", "Cancelled"];
const ROLES: MemberRole[] = ["Athlete", "Coach", "Referee", "ClubAdmin", "FederationStaff", "Visitor"];

const STATUS_STYLE: Record<BroadcastStatus, string> = {
  Draft:     "bg-slate-100 text-slate-700 border-slate-300",
  Scheduled: "bg-amber-50 text-amber-800 border-amber-200",
  Sending:   "bg-blue-50 text-blue-800 border-blue-200",
  Sent:      "bg-emerald-50 text-emerald-800 border-emerald-200",
  Failed:    "bg-red-50 text-red-700 border-red-200",
  Cancelled: "bg-slate-50 text-slate-500 border-slate-200",
};

export function BroadcastsPage() {
  const [page, setPage] = useState(1);
  const [statusFilter, setStatusFilter] = useState<BroadcastStatus | "">("");
  const [search, setSearch] = useState("");
  const [data, setData] = useState<PagedResult<BroadcastSummary> | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [creatorOpen, setCreatorOpen] = useState(false);

  const reload = useCallback(async () => {
    setLoading(true); setError(null);
    try {
      const res = await listBroadcasts({
        page, pageSize: 25,
        status: statusFilter || undefined,
        search: search.trim() || undefined,
      });
      setData(res);
    } catch (err) { setError((err as Error).message); }
    finally { setLoading(false); }
  }, [page, statusFilter, search]);

  useEffect(() => { reload(); }, [reload]);

  async function handleSend(id: string) {
    setBusyId(id);
    try {
      await sendBroadcast(id);
      await reload();
    } catch (err) { alert((err as Error).message); }
    finally { setBusyId(null); }
  }
  async function handleCancel(id: string) {
    if (!confirm("Cancel this campaign?")) return;
    setBusyId(id);
    try {
      await cancelBroadcast(id);
      await reload();
    } catch (err) { alert((err as Error).message); }
    finally { setBusyId(null); }
  }

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-slate-900">Broadcast campaigns</h1>
          <p className="mt-1 text-sm text-slate-500">
            Compose multi-channel announcements (email · SMS · push) and send them to filtered member groups.
          </p>
        </div>
        <button
          type="button"
          onClick={() => setCreatorOpen(true)}
          className="rounded-lg bg-brand-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-brand-700"
        >
          New campaign
        </button>
      </header>

      <section className="rounded-2xl border border-slate-200 bg-white shadow-sm">
        <div className="flex flex-wrap items-center gap-2 border-b border-slate-200 p-4">
          <select
            value={statusFilter}
            onChange={(e) => { setStatusFilter(e.target.value as BroadcastStatus | ""); setPage(1); }}
            className="rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-sm"
          >
            <option value="">All statuses</option>
            {STATUSES.map((s) => <option key={s} value={s}>{s}</option>)}
          </select>
          <input
            type="search"
            value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }}
            placeholder="Search title or subject…"
            className="rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-sm"
          />
          <button
            type="button"
            onClick={() => reload()}
            className="ml-auto rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-50"
          >Refresh</button>
        </div>

        {error && <div className="border-b border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>}

        <div className="overflow-x-auto">
          <table className="min-w-full text-sm">
            <thead className="bg-slate-50 text-xs font-semibold uppercase tracking-wide text-slate-500">
              <tr>
                <th className="px-4 py-3 text-left">Title</th>
                <th className="px-4 py-3 text-left">Channels</th>
                <th className="px-4 py-3 text-left">Status</th>
                <th className="px-4 py-3 text-right">Targets</th>
                <th className="px-4 py-3 text-right">Delivered</th>
                <th className="px-4 py-3 text-right">Failed</th>
                <th className="px-4 py-3 text-left">Created</th>
                <th className="px-4 py-3 text-right"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-200">
              {loading && !data && (
                <tr><td colSpan={8} className="px-4 py-6 text-center text-slate-500">Loading…</td></tr>
              )}
              {data?.items.length === 0 && !loading && (
                <tr><td colSpan={8} className="px-4 py-6 text-center text-slate-500">No campaigns yet.</td></tr>
              )}
              {data?.items.map((b) => (
                <tr key={b.id} className="hover:bg-slate-50">
                  <td className="px-4 py-3">
                    <div className="font-medium text-slate-900">{b.title}</div>
                    <div className="text-xs text-slate-500">{b.subject}</div>
                  </td>
                  <td className="px-4 py-3">
                    <span className="text-xs text-slate-700">{b.channels}</span>
                  </td>
                  <td className="px-4 py-3">
                    <span className={`inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium ${STATUS_STYLE[b.status]}`}>
                      {b.status}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-right tabular-nums">{b.totalTargets}</td>
                  <td className="px-4 py-3 text-right tabular-nums text-emerald-700">{b.deliveredCount}</td>
                  <td className="px-4 py-3 text-right tabular-nums text-red-700">{b.failedCount}</td>
                  <td className="px-4 py-3 text-xs text-slate-500">{new Date(b.createdAtUtc).toLocaleString()}</td>
                  <td className="px-4 py-3 text-right">
                    <div className="flex justify-end gap-1">
                      {(b.status === "Draft" || b.status === "Scheduled" || b.status === "Failed") && (
                        <button
                          type="button"
                          onClick={() => handleSend(b.id)}
                          disabled={busyId === b.id}
                          className="rounded-lg bg-brand-600 px-2.5 py-1 text-xs font-semibold text-white hover:bg-brand-700 disabled:opacity-50"
                        >
                          {busyId === b.id ? "Sending…" : "Send"}
                        </button>
                      )}
                      {(b.status === "Draft" || b.status === "Scheduled") && (
                        <button
                          type="button"
                          onClick={() => handleCancel(b.id)}
                          disabled={busyId === b.id}
                          className="rounded-lg border border-slate-200 px-2.5 py-1 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50"
                        >
                          Cancel
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {data && data.totalCount > data.pageSize && (
          <div className="flex items-center justify-between border-t border-slate-200 px-4 py-3">
            <div className="text-xs text-slate-500">
              Page {data.page} of {Math.max(1, Math.ceil(data.totalCount / data.pageSize))} · {data.totalCount} total
            </div>
            <div className="flex gap-2">
              <button type="button" disabled={page <= 1}
                onClick={() => setPage((p) => p - 1)}
                className="rounded-lg border border-slate-200 px-3 py-1 text-sm disabled:opacity-50">Prev</button>
              <button type="button" disabled={page * data.pageSize >= data.totalCount}
                onClick={() => setPage((p) => p + 1)}
                className="rounded-lg border border-slate-200 px-3 py-1 text-sm disabled:opacity-50">Next</button>
            </div>
          </div>
        )}
      </section>

      {creatorOpen && (
        <BroadcastCreatorModal
          onClose={() => setCreatorOpen(false)}
          onCreated={() => { setCreatorOpen(false); reload(); }}
        />
      )}
    </div>
  );
}

interface CreatorProps {
  onClose: () => void;
  onCreated: () => void;
}

function BroadcastCreatorModal({ onClose, onCreated }: CreatorProps) {
  const [title, setTitle] = useState("");
  const [subject, setSubject] = useState("");
  const [body, setBody] = useState("");
  const [emailOn, setEmailOn] = useState(true);
  const [smsOn,   setSmsOn]   = useState(false);
  const [pushOn,  setPushOn]  = useState(false);
  const [activeOnly, setActiveOnly] = useState(true);
  const [selectedRoles, setSelectedRoles] = useState<MemberRole[]>([]);
  const [targetClub,    setTargetClub]    = useState("");
  const [targetEvent,   setTargetEvent]   = useState("");
  const [preview, setPreview] = useState<{ total: number; email: number; sms: number; push: number } | null>(null);
  const [previewing, setPreviewing] = useState(false);
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const channels = useMemo<BroadcastChannel>(() => {
    const list: string[] = [];
    if (emailOn) list.push("Email");
    if (smsOn)   list.push("Sms");
    if (pushOn)  list.push("Push");
    if (list.length === 3) return "All";
    return (list.join(", ") as BroadcastChannel) || "None";
  }, [emailOn, smsOn, pushOn]);

  function toggleRole(role: MemberRole) {
    setSelectedRoles((prev) => prev.includes(role) ? prev.filter((r) => r !== role) : [...prev, role]);
  }

  async function runPreview() {
    setPreviewing(true); setError(null);
    try {
      const result = await previewBroadcastAudience({
        channels,
        targetRoles: selectedRoles.length ? selectedRoles : undefined,
        targetClubId:  targetClub.trim()  || null,
        targetEventId: targetEvent.trim() || null,
        activeMembersOnly: activeOnly,
      });
      setPreview({ total: result.totalMembers, email: result.emailReachable, sms: result.smsReachable, push: result.pushReachable });
    } catch (err) { setError((err as Error).message); }
    finally { setPreviewing(false); }
  }

  async function submit(send: boolean) {
    setCreating(true); setError(null);
    try {
      const payload: CreateBroadcastInput = {
        title: title.trim(),
        subject: subject.trim(),
        body,
        channels,
        targetRoles: selectedRoles.length ? selectedRoles : undefined,
        targetClubId:  targetClub.trim()  || null,
        targetEventId: targetEvent.trim() || null,
        activeMembersOnly: activeOnly,
      };
      const created = await createBroadcast(payload);
      if (send) await sendBroadcast(created.id);
      onCreated();
    } catch (err) { setError((err as Error).message); }
    finally { setCreating(false); }
  }

  const channelInvalid = channels === "None";

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4">
      <div className="w-full max-w-3xl overflow-hidden rounded-2xl bg-white shadow-2xl">
        <header className="flex items-center justify-between border-b border-slate-200 px-6 py-4">
          <h2 className="text-lg font-semibold text-slate-900">New broadcast campaign</h2>
          <button type="button" onClick={onClose}
            className="text-slate-500 hover:text-slate-900">&times;</button>
        </header>

        <div className="grid gap-4 p-6 md:grid-cols-2">
          <label className="md:col-span-2 text-sm">
            <span className="font-medium text-slate-700">Title</span>
            <input value={title} onChange={(e) => setTitle(e.target.value)}
              placeholder="Eid Al-Fitr coaches' training reminder"
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2" />
          </label>
          <label className="md:col-span-2 text-sm">
            <span className="font-medium text-slate-700">Subject</span>
            <input value={subject} onChange={(e) => setSubject(e.target.value)}
              placeholder="Subject line / push title"
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2" />
          </label>
          <label className="md:col-span-2 text-sm">
            <span className="font-medium text-slate-700">Message</span>
            <textarea value={body} onChange={(e) => setBody(e.target.value)} rows={6}
              placeholder="Plain text body. SMS senders truncate at the gateway's limit; push notifications use the first ~150 chars."
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2" />
          </label>

          <fieldset className="md:col-span-2 rounded-xl border border-slate-200 p-4">
            <legend className="px-2 text-xs font-semibold uppercase tracking-wide text-slate-500">Channels</legend>
            <div className="flex flex-wrap gap-4 text-sm">
              <label className="flex items-center gap-2">
                <input type="checkbox" checked={emailOn} onChange={(e) => setEmailOn(e.target.checked)} />
                Email
              </label>
              <label className="flex items-center gap-2">
                <input type="checkbox" checked={smsOn} onChange={(e) => setSmsOn(e.target.checked)} />
                SMS
              </label>
              <label className="flex items-center gap-2">
                <input type="checkbox" checked={pushOn} onChange={(e) => setPushOn(e.target.checked)} />
                Push
              </label>
              {channelInvalid && (
                <span className="text-xs text-red-600">Pick at least one channel.</span>
              )}
            </div>
          </fieldset>

          <fieldset className="md:col-span-2 rounded-xl border border-slate-200 p-4">
            <legend className="px-2 text-xs font-semibold uppercase tracking-wide text-slate-500">Audience</legend>
            <div className="grid gap-4 md:grid-cols-2">
              <div>
                <div className="text-xs font-medium text-slate-500">Roles (empty = all)</div>
                <div className="mt-2 flex flex-wrap gap-2 text-xs">
                  {ROLES.map((r) => (
                    <button key={r} type="button"
                      onClick={() => toggleRole(r)}
                      className={`rounded-full border px-3 py-1 ${selectedRoles.includes(r)
                        ? "border-brand-600 bg-brand-50 text-brand-700"
                        : "border-slate-200 text-slate-600 hover:bg-slate-50"}`}>
                      {r}
                    </button>
                  ))}
                </div>
              </div>
              <label className="flex items-center gap-2 self-start text-sm">
                <input type="checkbox" checked={activeOnly} onChange={(e) => setActiveOnly(e.target.checked)} />
                Active members only
              </label>
              <label className="text-sm">
                <span className="font-medium text-slate-700">Club id (optional)</span>
                <input value={targetClub} onChange={(e) => setTargetClub(e.target.value)}
                  className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2" />
              </label>
              <label className="text-sm">
                <span className="font-medium text-slate-700">Event id (optional)</span>
                <input value={targetEvent} onChange={(e) => setTargetEvent(e.target.value)}
                  className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2" />
              </label>
            </div>

            <div className="mt-4 flex flex-wrap items-center gap-3 border-t border-slate-100 pt-4">
              <button type="button" onClick={runPreview} disabled={previewing}
                className="rounded-lg border border-slate-200 px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50">
                {previewing ? "Calculating…" : "Preview audience"}
              </button>
              {preview && (
                <span className="text-xs text-slate-600">
                  <strong>{preview.total}</strong> match · email <strong>{preview.email}</strong>{" "}
                  · SMS <strong>{preview.sms}</strong> · push <strong>{preview.push}</strong>
                </span>
              )}
            </div>
          </fieldset>

          {error && <div className="md:col-span-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{error}</div>}
        </div>

        <footer className="flex items-center justify-end gap-2 border-t border-slate-200 px-6 py-4">
          <button type="button" onClick={onClose}
            className="rounded-lg border border-slate-200 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50">
            Cancel
          </button>
          <button type="button" onClick={() => submit(false)} disabled={creating || channelInvalid}
            className="rounded-lg border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50">
            Save draft
          </button>
          <button type="button" onClick={() => submit(true)} disabled={creating || channelInvalid}
            className="rounded-lg bg-brand-600 px-3 py-2 text-sm font-semibold text-white hover:bg-brand-700 disabled:opacity-50">
            {creating ? "Sending…" : "Save & send now"}
          </button>
        </footer>
      </div>
    </div>
  );
}
