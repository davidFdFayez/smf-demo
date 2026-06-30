import { useEffect, useState } from "react";
import clsx from "clsx";
import {
  cancelEvent,
  cancelRegistration,
  checkInRegistration,
  createEvent,
  listEventRegistrations,
  listEvents,
  markNoShow,
  publishEvent,
  updateEvent,
  type CreateEventInput,
  type EventRegistrationSummary,
  type EventSummary,
  type LiveStreamProvider,
  type UpdateEventInput,
} from "../services/eventsApi";
import { downloadCsv } from "../services/exportApi";
import {
  clearEventLiveStream,
  setEventLiveStream,
} from "../services/liveStreamsApi";

function defaultForm(): CreateEventInput {
  const now = new Date();
  const start = new Date(now); start.setUTCDate(start.getUTCDate() + 30);
  const end = new Date(start); end.setUTCDate(end.getUTCDate() + 1);
  const regOpens = new Date(now);
  const regCloses = new Date(start); regCloses.setUTCDate(regCloses.getUTCDate() - 3);
  const iso = (d: Date) => d.toISOString().slice(0, 16);
  return {
    title: "", description: "", location: "",
    startsAtUtc: iso(start), endsAtUtc: iso(end),
    registrationOpensAtUtc: iso(regOpens), registrationClosesAtUtc: iso(regCloses),
    entryFeeMinor: 0, currency: "SAR", capacity: null,
  };
}

export function EventsPage() {
  const [events, setEvents] = useState<EventSummary[]>([]);
  const [form, setForm] = useState<CreateEventInput>(defaultForm);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showForm, setShowForm] = useState(false);

  const load = async () => {
    try { setEvents(await listEvents()); setError(null); }
    catch (e) { setError((e as Error).message); }
  };
  useEffect(() => { void load(); }, []);

  const update = <K extends keyof CreateEventInput>(k: K) =>
    (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
      const v =
        k === "entryFeeMinor" || k === "capacity"
          ? e.target.value === "" ? (k === "capacity" ? null : 0) : Number(e.target.value)
          : e.target.value;
      setForm((f) => ({ ...f, [k]: v as CreateEventInput[K] }));
    };

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true); setError(null);
    try {
      const toUtc = (s: string) => (s.endsWith("Z") ? s : `${s}:00Z`);
      await createEvent({
        ...form,
        description: form.description || undefined,
        startsAtUtc: toUtc(form.startsAtUtc),
        endsAtUtc: toUtc(form.endsAtUtc),
        registrationOpensAtUtc: toUtc(form.registrationOpensAtUtc),
        registrationClosesAtUtc: toUtc(form.registrationClosesAtUtc),
      });
      setForm(defaultForm()); setShowForm(false); await load();
    } catch (e) { setError((e as Error).message); }
    finally { setSubmitting(false); }
  };

  const df = new Intl.DateTimeFormat("en-US", { dateStyle: "medium", timeStyle: "short" });

  return (
    <div className="space-y-5">
      <div className="flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h2 className="text-xl font-semibold text-slate-900">Events</h2>
          <p className="text-sm text-slate-500">{events.length} total</p>
        </div>
        <div className="flex gap-2">
          <button onClick={() => void downloadCsv("events")} className="btn-secondary">Download CSV</button>
          <button onClick={() => setShowForm((v) => !v)} className="btn-primary">
            {showForm ? "Close form" : "+ Create event"}
          </button>
        </div>
      </div>

      {showForm && (
        <form onSubmit={submit} className="card space-y-4 p-5">
          <h3 className="text-sm font-semibold text-slate-900">Create event</h3>
          <div className="grid gap-4 md:grid-cols-2">
            <Field label="Title" required><input required className="admin-input" value={form.title} onChange={update("title")} /></Field>
            <Field label="Location" required><input required className="admin-input" value={form.location} onChange={update("location")} /></Field>
            <Field label="Description" className="md:col-span-2">
              <textarea className="admin-input min-h-[60px]" value={form.description} onChange={update("description")} />
            </Field>
            <Field label="Starts (UTC)"><input type="datetime-local" className="admin-input" value={form.startsAtUtc} onChange={update("startsAtUtc")} /></Field>
            <Field label="Ends (UTC)"><input type="datetime-local" className="admin-input" value={form.endsAtUtc} onChange={update("endsAtUtc")} /></Field>
            <Field label="Registration opens"><input type="datetime-local" className="admin-input" value={form.registrationOpensAtUtc} onChange={update("registrationOpensAtUtc")} /></Field>
            <Field label="Registration closes"><input type="datetime-local" className="admin-input" value={form.registrationClosesAtUtc} onChange={update("registrationClosesAtUtc")} /></Field>
            <Field label="Entry fee (minor units)"><input type="number" min={0} className="admin-input" value={form.entryFeeMinor} onChange={update("entryFeeMinor")} /></Field>
            <Field label="Currency"><input maxLength={3} className="admin-input" value={form.currency} onChange={update("currency")} /></Field>
            <Field label="Capacity"><input type="number" min={1} className="admin-input" value={form.capacity ?? ""} onChange={update("capacity")} /></Field>
          </div>
          {error && <div className="rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-800">{error}</div>}
          <button type="submit" disabled={submitting} className="btn-primary">{submitting ? "Saving…" : "Create event"}</button>
        </form>
      )}

      {error && !showForm && <div className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-800">{error}</div>}

      {events.length === 0 ? (
        <div className="card p-8 text-center text-sm text-slate-500">No events yet.</div>
      ) : (
        <ul className="space-y-3">
          {events.map((e) => (
            <li key={e.id} className="card p-5">
              <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                <div className="min-w-0">
                  <div className="flex items-center gap-2">
                    <h3 className="truncate text-base font-semibold text-slate-900">{e.title}</h3>
                    <EventStatusPill status={e.status} />
                  </div>
                  <div className="mt-0.5 text-xs text-slate-500">{e.location}</div>
                  {e.description && <p className="mt-2 text-sm text-slate-700">{e.description}</p>}
                  <div className="mt-3 text-xs text-slate-600">
                    {df.format(new Date(e.startsAtUtc))} → {df.format(new Date(e.endsAtUtc))}
                  </div>
                  <div className="text-xs text-slate-500">
                    {e.registrationCount} registered · capacity {e.capacity ?? "∞"} · fee {(e.entryFeeMinor / 100).toFixed(2)} {e.currency}
                  </div>
                </div>
                <div className="flex flex-col gap-2 sm:items-end">
                  {e.status === "Draft" && (
                    <button onClick={async () => { await publishEvent(e.id); await load(); }} className="btn-primary text-xs">Publish</button>
                  )}
                  {e.status !== "Cancelled" && e.status !== "Completed" && (
                    <button onClick={async () => { await cancelEvent(e.id); await load(); }} className="btn-secondary text-xs">Cancel</button>
                  )}
                </div>
              </div>
              <EventInlineEditor event={e} onChanged={load} />
              <LiveStreamInlineEditor event={e} onChanged={load} />
              <RegistrationsPanel event={e} />
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function EventInlineEditor({
  event,
  onChanged,
}: {
  event: EventSummary;
  onChanged: () => Promise<void>;
}) {
  const [open, setOpen] = useState(false);
  const toLocal = (iso: string) => iso.slice(0, 16);
  const [form, setForm] = useState<UpdateEventInput>({
    title: event.title,
    description: event.description ?? "",
    location: event.location,
    startsAtUtc: toLocal(event.startsAtUtc),
    endsAtUtc: toLocal(event.endsAtUtc),
    registrationOpensAtUtc: toLocal(event.registrationOpensAtUtc),
    registrationClosesAtUtc: toLocal(event.registrationClosesAtUtc),
    entryFeeMinor: event.entryFeeMinor,
    currency: event.currency,
    capacity: event.capacity ?? null,
  });
  const [busy, setBusy] = useState(false);
  const [msg, setMsg] = useState<string | null>(null);

  // Can't edit completed/cancelled events — backend enforces this anyway.
  const locked = event.status === "Completed" || event.status === "Cancelled";

  const change = <K extends keyof UpdateEventInput>(k: K) =>
    (ev: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
      const v =
        k === "entryFeeMinor" || k === "capacity"
          ? ev.target.value === ""
            ? k === "capacity"
              ? null
              : 0
            : Number(ev.target.value)
          : ev.target.value;
      setForm((f) => ({ ...f, [k]: v as UpdateEventInput[K] }));
    };

  const save = async (ev: React.FormEvent) => {
    ev.preventDefault();
    setBusy(true); setMsg(null);
    try {
      const toUtc = (s: string) => (s.endsWith("Z") ? s : `${s}:00Z`);
      await updateEvent(event.id, {
        ...form,
        description: form.description || null,
        startsAtUtc: toUtc(form.startsAtUtc),
        endsAtUtc: toUtc(form.endsAtUtc),
        registrationOpensAtUtc: toUtc(form.registrationOpensAtUtc),
        registrationClosesAtUtc: toUtc(form.registrationClosesAtUtc),
      });
      setMsg("Saved"); setOpen(false); await onChanged();
    } catch (e) { setMsg((e as Error).message); }
    finally { setBusy(false); }
  };

  return (
    <div className="mt-4 rounded-xl border border-dashed border-slate-200 bg-slate-50/60 p-3">
      <div className="flex items-center justify-between">
        <span className="text-xs font-semibold uppercase tracking-wide text-slate-500">
          Edit details
        </span>
        <button
          type="button"
          onClick={() => setOpen((v) => !v)}
          disabled={locked}
          className="btn-secondary text-xs disabled:opacity-40"
        >
          {locked ? "Locked" : open ? "Close" : "Edit"}
        </button>
      </div>
      {open && !locked && (
        <form onSubmit={save} className="mt-3 grid gap-3 sm:grid-cols-2">
          <Field label="Title"><input required className="admin-input text-xs" value={form.title} onChange={change("title")} /></Field>
          <Field label="Location"><input required className="admin-input text-xs" value={form.location} onChange={change("location")} /></Field>
          <Field label="Description" className="sm:col-span-2">
            <textarea className="admin-input min-h-[60px] text-xs" value={form.description ?? ""} onChange={change("description")} />
          </Field>
          <Field label="Starts"><input type="datetime-local" className="admin-input text-xs" value={form.startsAtUtc} onChange={change("startsAtUtc")} /></Field>
          <Field label="Ends"><input type="datetime-local" className="admin-input text-xs" value={form.endsAtUtc} onChange={change("endsAtUtc")} /></Field>
          <Field label="Reg opens"><input type="datetime-local" className="admin-input text-xs" value={form.registrationOpensAtUtc} onChange={change("registrationOpensAtUtc")} /></Field>
          <Field label="Reg closes"><input type="datetime-local" className="admin-input text-xs" value={form.registrationClosesAtUtc} onChange={change("registrationClosesAtUtc")} /></Field>
          <Field label="Fee (minor)"><input type="number" min={0} className="admin-input text-xs" value={form.entryFeeMinor} onChange={change("entryFeeMinor")} /></Field>
          <Field label="Currency"><input maxLength={3} className="admin-input text-xs" value={form.currency} onChange={change("currency")} /></Field>
          <Field label="Capacity"><input type="number" min={1} className="admin-input text-xs" value={form.capacity ?? ""} onChange={change("capacity")} /></Field>
          <div className="sm:col-span-2 flex items-center gap-3">
            <button type="submit" disabled={busy} className="btn-primary text-xs">
              {busy ? "Saving…" : "Save changes"}
            </button>
            {msg && <span className="text-xs text-slate-500">{msg}</span>}
          </div>
        </form>
      )}
    </div>
  );
}

function RegistrationsPanel({ event }: { event: EventSummary }) {
  const [open, setOpen] = useState(false);
  const [regs, setRegs] = useState<EventRegistrationSummary[]>([]);
  const [loading, setLoading] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  const refresh = async () => {
    setLoading(true); setErr(null);
    try {
      setRegs(await listEventRegistrations(event.id));
    } catch (e) { setErr((e as Error).message); }
    finally { setLoading(false); }
  };

  useEffect(() => {
    if (open) void refresh();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, event.id]);

  const act = async (fn: () => Promise<void>) => {
    try { await fn(); await refresh(); }
    catch (e) { setErr((e as Error).message); }
  };

  return (
    <div className="mt-3 rounded-xl border border-dashed border-slate-200 bg-slate-50/60 p-3">
      <div className="flex items-center justify-between">
        <span className="text-xs font-semibold uppercase tracking-wide text-slate-500">
          Registrations ({event.registrationCount})
        </span>
        <button
          type="button"
          onClick={() => setOpen((v) => !v)}
          className="btn-secondary text-xs"
        >
          {open ? "Hide" : "Show"}
        </button>
      </div>

      {open && (
        <div className="mt-3">
          {loading && <div className="text-xs text-slate-500">Loading…</div>}
          {err && (
            <div className="rounded-lg border border-rose-200 bg-rose-50 p-2 text-xs text-rose-800">
              {err}
            </div>
          )}
          {!loading && regs.length === 0 && (
            <div className="text-xs text-slate-500">No registrations yet.</div>
          )}
          {regs.length > 0 && (
            <table className="w-full text-xs">
              <thead>
                <tr className="text-left text-slate-500">
                  <th className="pb-1 font-medium">Member</th>
                  <th className="pb-1 font-medium">Status</th>
                  <th className="pb-1 font-medium">Registered</th>
                  <th className="pb-1 font-medium"></th>
                </tr>
              </thead>
              <tbody>
                {regs.map((r) => (
                  <tr key={r.id} className="border-t border-slate-200">
                    <td className="py-1.5">{r.memberName}</td>
                    <td className="py-1.5">
                      <RegStatus status={r.status} />
                    </td>
                    <td className="py-1.5 font-mono text-[11px] text-slate-500">
                      {new Date(r.registeredAtUtc).toLocaleString()}
                    </td>
                    <td className="py-1.5">
                      <div className="flex justify-end gap-1">
                        {r.status === "Confirmed" && (
                          <button className="btn-secondary text-[11px]" onClick={() => act(() => checkInRegistration(r.id))}>
                            Check-in
                          </button>
                        )}
                        {r.status === "Confirmed" && (
                          <button className="btn-secondary text-[11px]" onClick={() => act(() => markNoShow(r.id))}>
                            No-show
                          </button>
                        )}
                        {(r.status === "PendingPayment" || r.status === "Confirmed") && (
                          <button className="btn-secondary text-[11px]" onClick={() => act(() => cancelRegistration(r.id))}>
                            Cancel
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}
    </div>
  );
}

function RegStatus({ status }: { status: EventRegistrationSummary["status"] }) {
  const cls: Record<EventRegistrationSummary["status"], string> = {
    PendingPayment: "bg-amber-50 text-amber-800 ring-amber-400/40",
    Confirmed: "bg-emerald-50 text-emerald-700 ring-emerald-500/30",
    CheckedIn: "bg-sky-50 text-sky-700 ring-sky-500/30",
    NoShow: "bg-slate-100 text-slate-600 ring-slate-400/30",
    Cancelled: "bg-rose-50 text-rose-700 ring-rose-500/30",
  };
  return <span className={`inline-flex rounded-full px-2 py-0.5 text-[10px] font-medium ring-1 ring-inset ${cls[status]}`}>{status}</span>;
}

function Field({ label, required, children, className }: { label: string; required?: boolean; children: React.ReactNode; className?: string }) {
  return (
    <label className={clsx("block text-sm", className)}>
      <span className="admin-label">{label}{required && <span className="ml-1 text-rose-500">*</span>}</span>
      <div className="mt-1">{children}</div>
    </label>
  );
}

function LiveStreamInlineEditor({
  event,
  onChanged,
}: {
  event: EventSummary;
  onChanged: () => Promise<void>;
}) {
  const [provider, setProvider] = useState<LiveStreamProvider>(
    event.liveStreamProvider ?? "YouTube",
  );
  const [url, setUrl] = useState(event.liveStreamUrl ?? "");
  const [busy, setBusy] = useState(false);
  const [msg, setMsg] = useState<string | null>(null);

  const save = async () => {
    setBusy(true); setMsg(null);
    try {
      await setEventLiveStream(event.id, { provider, url });
      setMsg("Saved"); await onChanged();
    } catch (e) { setMsg((e as Error).message); }
    finally { setBusy(false); }
  };
  const clear = async () => {
    setBusy(true); setMsg(null);
    try {
      await clearEventLiveStream(event.id);
      setUrl(""); setMsg("Cleared"); await onChanged();
    } catch (e) { setMsg((e as Error).message); }
    finally { setBusy(false); }
  };

  return (
    <div className="mt-4 rounded-xl border border-dashed border-slate-200 bg-slate-50/60 p-3">
      <div className="flex flex-wrap items-center gap-2 text-xs text-slate-600">
        <span className="font-semibold uppercase tracking-wide text-slate-500">
          Live stream
        </span>
        {event.liveStreamUrl ? (
          <span className="chip bg-emerald-50 text-emerald-700 ring-emerald-500/30">
            {event.liveStreamProvider}
          </span>
        ) : (
          <span className="chip bg-slate-100 text-slate-600 ring-slate-400/30">
            Not set
          </span>
        )}
      </div>
      <div className="mt-2 grid gap-2 sm:grid-cols-[140px_1fr_auto_auto]">
        <select
          value={provider}
          onChange={(e) => setProvider(e.target.value as LiveStreamProvider)}
          className="admin-input text-xs"
        >
          <option value="YouTube">YouTube</option>
          <option value="Twitch">Twitch</option>
          <option value="Vimeo">Vimeo</option>
          <option value="CustomEmbed">Custom embed</option>
        </select>
        <input
          type="url"
          value={url}
          placeholder="https://www.youtube.com/watch?v=…"
          onChange={(e) => setUrl(e.target.value)}
          className="admin-input text-xs"
        />
        <button
          type="button"
          onClick={save}
          disabled={busy || !url.trim()}
          className="btn-primary text-xs"
        >
          {busy ? "Saving…" : "Save"}
        </button>
        <button
          type="button"
          onClick={clear}
          disabled={busy || !event.liveStreamUrl}
          className="btn-secondary text-xs"
        >
          Clear
        </button>
      </div>
      {msg && <div className="mt-1 text-[11px] text-slate-500">{msg}</div>}
    </div>
  );
}

function EventStatusPill({ status }: { status: EventSummary["status"] }) {
  const cls: Record<EventSummary["status"], string> = {
    Draft: "bg-slate-100 text-slate-700 ring-slate-500/20",
    Published: "bg-sky-50 text-sky-700 ring-sky-500/30",
    RegistrationOpen: "bg-emerald-50 text-emerald-700 ring-emerald-500/30",
    RegistrationClosed: "bg-amber-50 text-amber-800 ring-amber-400/40",
    InProgress: "bg-indigo-50 text-indigo-700 ring-indigo-500/30",
    Completed: "bg-slate-50 text-slate-600 ring-slate-400/30",
    Cancelled: "bg-rose-50 text-rose-700 ring-rose-500/30",
  };
  return <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${cls[status]}`}>{status}</span>;
}
