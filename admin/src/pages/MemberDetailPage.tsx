import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import {
  approveMember,
  getMemberById,
  issueDigitalId,
  recordMedicalClearance,
  type MemberDetails,
} from "../services/membersApi";

export function MemberDetailPage() {
  const { id = "" } = useParams();
  const [m, setM] = useState<MemberDetails | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [token, setToken] = useState<string | null>(null);
  const [weightInput, setWeightInput] = useState<string>("");

  const load = async () => {
    setLoading(true);
    try {
      const res = await getMemberById(id);
      setM(res);
      setWeightInput(res.weightCategoryKg?.toString() ?? "");
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { void load(); /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, [id]);

  if (loading && !m) return <div className="card p-6 text-sm text-slate-500">Loading…</div>;
  if (error) return <div className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-800">{error}</div>;
  if (!m) return null;

  const approve = async () => { try { await approveMember(m.id); await load(); } catch (e) { setError((e as Error).message); } };
  const setMedical = async (cleared: boolean) => {
    try {
      const kg = Number(weightInput);
      await recordMedicalClearance(m.id, {
        cleared,
        weightCategoryKg: Number.isFinite(kg) && kg > 0 ? kg : undefined,
      });
      await load();
    } catch (e) { setError((e as Error).message); }
  };
  const issueToken = async () => {
    try { const r = await issueDigitalId(m.id); setToken(r.token); }
    catch (e) { setError((e as Error).message); }
  };

  return (
    <div className="space-y-5">
      <div className="flex items-center gap-3 text-sm">
        <Link to="/members" className="text-brand-700 hover:text-brand-800">← All members</Link>
      </div>

      <div className="card p-6">
        <div className="flex flex-col gap-4 md:flex-row md:items-start md:justify-between">
          <div>
            <div className="text-xs uppercase tracking-wide text-slate-500">SMF ID</div>
            <div className="mt-0.5 font-mono text-sm text-slate-700">{m.smF_ID}</div>
            <h2 className="mt-3 text-2xl font-bold text-slate-900">{m.fullName}</h2>
            <div className="mt-1 text-sm text-slate-500">
              {m.role} · born {m.dateOfBirth} · registered {new Date(m.createdAtUtc).toLocaleDateString()}
            </div>
          </div>
          <div className="flex flex-col items-end gap-2">
            <StatusPill status={m.registrationStatus} />
            {m.registrationStatus === "Pending" && (
              <button onClick={approve} className="btn-primary text-xs">Approve registration</button>
            )}
          </div>
        </div>

        <dl className="mt-6 grid gap-4 text-sm md:grid-cols-2">
          <Row label="Email" value={m.email || "—"} />
          <Row label="Phone" value={m.phoneNumber || "—"} />
          <Row label="National ID" value={m.nationalId} mono />
          <Row label="Guardian consent" value={m.guardianConsent ? "Yes" : "No"} />
          <Row label="Affiliated club" value={m.affiliatedClubId ?? "—"} mono />
          <Row label="License level" value={m.licenseLevel ?? "—"} />
          <Row label="Years of experience" value={m.yearsOfExperience?.toString() ?? "—"} />
        </dl>
      </div>

      {m.role === "Athlete" && (
        <div className="card p-6">
          <h3 className="text-sm font-semibold text-slate-900">Athlete readiness</h3>
          <p className="text-xs text-slate-500">Medical clearance gates tournament participation.</p>
          <div className="mt-4 grid gap-3 md:grid-cols-[1fr_auto_auto]">
            <label className="block">
              <span className="admin-label">Weight category (kg)</span>
              <input
                type="number" step="0.1" min="1" max="300"
                value={weightInput}
                onChange={(e) => setWeightInput(e.target.value)}
                className="admin-input"
                placeholder="e.g. 67.5"
              />
            </label>
            <button className="btn-primary self-end" onClick={() => void setMedical(true)}>Mark cleared</button>
            <button className="btn-secondary self-end" onClick={() => void setMedical(false)}>Mark not cleared</button>
          </div>
          <div className="mt-3 text-xs text-slate-500">
            Current status:{" "}
            <span className="font-medium text-slate-700">
              {m.medicalCleared === true ? "Cleared" : m.medicalCleared === false ? "Not cleared" : "Pending"}
            </span>
            {m.medicalClearedAtUtc && ` · last updated ${new Date(m.medicalClearedAtUtc).toLocaleString()}`}
          </div>
        </div>
      )}

      <div className="card p-6">
        <div className="flex items-center justify-between">
          <div>
            <h3 className="text-sm font-semibold text-slate-900">Digital ID</h3>
            <p className="text-xs text-slate-500">Issues a signed JWT for offline venue verification.</p>
          </div>
          <button className="btn-primary text-xs" onClick={issueToken}>Issue Digital ID</button>
        </div>
        {token && (
          <textarea
            readOnly
            value={token}
            className="admin-input mt-4 h-24 font-mono text-[11px]"
          />
        )}
      </div>
    </div>
  );
}

function Row({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div>
      <dt className="admin-label">{label}</dt>
      <dd className={`mt-0.5 ${mono ? "font-mono text-xs" : "text-sm"} text-slate-800`}>{value}</dd>
    </div>
  );
}

function StatusPill({ status }: { status: string }) {
  const cls =
    status === "Approved" || status === "Active"
      ? "bg-emerald-50 text-emerald-700 ring-emerald-500/30"
      : "bg-amber-50 text-amber-800 ring-amber-400/40";
  return (
    <span className={`inline-flex items-center rounded-full px-3 py-1 text-sm font-medium ring-1 ring-inset ${cls}`}>
      {status}
    </span>
  );
}
