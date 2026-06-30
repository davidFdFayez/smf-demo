import { useEffect, useState } from "react";
import {
  listTenants,
  provisionTenant,
  setTenantActive,
  updateTenant,
  type ProvisionTenantInput,
  type ScoringTenantDto,
  type UpdateTenantInput,
} from "../services/tenantsApi";

const DEFAULT_NEW: ProvisionTenantInput = {
  code: "",
  displayName: "",
  primaryColor: "#0f172a",
  accentColor: "#22c55e",
  logoUrl: "",
  contactEmail: "",
};

export function TenantsPage() {
  const [tenants, setTenants] = useState<ScoringTenantDto[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState<ProvisionTenantInput>(DEFAULT_NEW);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const load = async () => {
    try { setTenants(await listTenants()); setError(null); }
    catch (e) { setError((e as Error).message); }
  };
  useEffect(() => { void load(); }, []);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault(); setBusy(true); setError(null);
    try {
      await provisionTenant({
        ...form,
        logoUrl: form.logoUrl?.trim() ? form.logoUrl : undefined,
      });
      setForm(DEFAULT_NEW); setShowForm(false); await load();
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  };

  return (
    <div className="space-y-5">
      <div className="flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h2 className="text-xl font-semibold text-slate-900">Scoring tenants</h2>
          <p className="text-sm text-slate-500">
            White-label broadcasters that re-brand the public scoreboard.
          </p>
        </div>
        <button className="btn-primary" onClick={() => setShowForm((v) => !v)}>
          {showForm ? "Close form" : "+ Provision tenant"}
        </button>
      </div>

      {error && <div className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-800">{error}</div>}

      {showForm && (
        <form className="card space-y-4 p-5" onSubmit={submit}>
          <div className="grid gap-4 md:grid-cols-2">
            <L label="Tenant code *">
              <input required minLength={2} className="admin-input"
                value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value.toUpperCase() })}
                placeholder="GULFSPORT" />
            </L>
            <L label="Display name *">
              <input required className="admin-input"
                value={form.displayName} onChange={(e) => setForm({ ...form, displayName: e.target.value })}
                placeholder="Gulf Sport Broadcasting" />
            </L>
            <L label="Contact email *">
              <input required type="email" className="admin-input"
                value={form.contactEmail} onChange={(e) => setForm({ ...form, contactEmail: e.target.value })} />
            </L>
            <L label="Logo URL">
              <input className="admin-input" value={form.logoUrl ?? ""}
                onChange={(e) => setForm({ ...form, logoUrl: e.target.value })} />
            </L>
            <L label="Primary color">
              <ColorRow color={form.primaryColor}
                onChange={(c) => setForm({ ...form, primaryColor: c })} />
            </L>
            <L label="Accent color">
              <ColorRow color={form.accentColor}
                onChange={(c) => setForm({ ...form, accentColor: c })} />
            </L>
          </div>
          <div className="flex gap-2">
            <button type="submit" disabled={busy} className="btn-primary">
              {busy ? "Saving…" : "Provision tenant"}
            </button>
            <button type="button" className="btn-secondary" onClick={() => setShowForm(false)}>
              Cancel
            </button>
          </div>
        </form>
      )}

      {tenants.length === 0 ? (
        <div className="card p-8 text-center text-sm text-slate-500">No tenants yet.</div>
      ) : (
        <ul className="grid gap-4 md:grid-cols-2">
          {tenants.map((t) => (
            <TenantCard key={t.id} tenant={t} onChanged={load} />
          ))}
        </ul>
      )}
    </div>
  );
}

function TenantCard({
  tenant,
  onChanged,
}: {
  tenant: ScoringTenantDto;
  onChanged: () => Promise<void>;
}) {
  const [editing, setEditing] = useState(false);
  const [form, setForm] = useState<UpdateTenantInput>({
    displayName: tenant.displayName,
    primaryColor: tenant.primaryColor,
    accentColor: tenant.accentColor,
    logoUrl: tenant.logoUrl ?? "",
    contactEmail: tenant.contactEmail,
  });
  const [busy, setBusy] = useState(false);

  const save = async (e: React.FormEvent) => {
    e.preventDefault(); setBusy(true);
    try {
      await updateTenant(tenant.id, {
        ...form,
        logoUrl: form.logoUrl?.trim() ? form.logoUrl : undefined,
      });
      setEditing(false); await onChanged();
    } finally { setBusy(false); }
  };

  const toggleActive = async () => {
    setBusy(true);
    try { await setTenantActive(tenant.id, !tenant.isActive); await onChanged(); }
    finally { setBusy(false); }
  };

  return (
    <li className="card overflow-hidden">
      <div
        className="h-20 w-full"
        style={{ background: `linear-gradient(135deg, ${tenant.primaryColor}, ${tenant.accentColor})` }}
      />
      <div className="p-5">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <div className="font-mono text-[11px] uppercase tracking-wide text-slate-500">
              {tenant.code}
            </div>
            <h3 className="truncate text-base font-semibold text-slate-900">{tenant.displayName}</h3>
            <div className="mt-0.5 truncate text-xs text-slate-500">{tenant.contactEmail}</div>
          </div>
          <span
            className={
              tenant.isActive
                ? "chip bg-emerald-50 text-emerald-700 ring-emerald-500/30"
                : "chip bg-slate-100 text-slate-600 ring-slate-400/30"
            }
          >
            {tenant.isActive ? "Active" : "Disabled"}
          </span>
        </div>

        <div className="mt-4 flex flex-wrap gap-2">
          <button className="btn-secondary text-xs" onClick={() => setEditing((v) => !v)}>
            {editing ? "Close" : "Edit brand"}
          </button>
          <button className="btn-secondary text-xs" disabled={busy} onClick={toggleActive}>
            {tenant.isActive ? "Deactivate" : "Activate"}
          </button>
          <a
            className="btn-secondary text-xs"
            target="_blank"
            rel="noreferrer"
            href={`/watch?tenant=${encodeURIComponent(tenant.code)}`}
          >
            Preview scoreboard
          </a>
        </div>

        {editing && (
          <form className="mt-4 space-y-3 rounded-xl border border-dashed border-slate-200 p-3" onSubmit={save}>
            <L label="Display name">
              <input className="admin-input text-sm" value={form.displayName}
                onChange={(e) => setForm({ ...form, displayName: e.target.value })} />
            </L>
            <L label="Contact email">
              <input className="admin-input text-sm" value={form.contactEmail}
                onChange={(e) => setForm({ ...form, contactEmail: e.target.value })} />
            </L>
            <L label="Logo URL">
              <input className="admin-input text-sm" value={form.logoUrl ?? ""}
                onChange={(e) => setForm({ ...form, logoUrl: e.target.value })} />
            </L>
            <div className="grid gap-3 sm:grid-cols-2">
              <L label="Primary color">
                <ColorRow color={form.primaryColor}
                  onChange={(c) => setForm({ ...form, primaryColor: c })} />
              </L>
              <L label="Accent color">
                <ColorRow color={form.accentColor}
                  onChange={(c) => setForm({ ...form, accentColor: c })} />
              </L>
            </div>
            <button type="submit" disabled={busy} className="btn-primary text-xs">
              {busy ? "Saving…" : "Save"}
            </button>
          </form>
        )}
      </div>
    </li>
  );
}

function L({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="block text-sm">
      <span className="admin-label">{label}</span>
      <div className="mt-1">{children}</div>
    </label>
  );
}

function ColorRow({
  color,
  onChange,
}: {
  color: string;
  onChange: (c: string) => void;
}) {
  return (
    <div className="flex items-center gap-2">
      <input
        type="color"
        value={color}
        onChange={(e) => onChange(e.target.value)}
        className="h-9 w-12 cursor-pointer rounded border border-slate-300"
      />
      <input
        className="admin-input text-sm font-mono"
        value={color}
        onChange={(e) => onChange(e.target.value)}
        pattern="^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$"
      />
    </div>
  );
}
