import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import clsx from "clsx";
import {
  MemberRole,
  RegistrationStatus,
  type MemberListItem,
  type PagedResult,
} from "../types/member";
import {
  ApiError,
  approveMember,
  listMembers,
  recordMedicalClearance,
} from "../services/membersApi";
import { downloadCsv } from "../services/exportApi";

const PAGE_SIZE = 25;

export function MembersPage() {
  const [data, setData] = useState<PagedResult<MemberListItem> | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [approvingId, setApprovingId] = useState<string | null>(null);
  const [exporting, setExporting] = useState(false);
  const [page, setPage] = useState(1);

  const [searchInput, setSearchInput] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [role, setRole] = useState("");
  const [status, setStatus] = useState("");

  useEffect(() => {
    const id = window.setTimeout(() => {
      setDebouncedSearch(searchInput.trim());
      setPage(1);
    }, 250);
    return () => window.clearTimeout(id);
  }, [searchInput]);

  const filters = useMemo(
    () => ({ search: debouncedSearch, role, status }),
    [debouncedSearch, role, status],
  );

  const load = useCallback(
    async (signal?: AbortSignal) => {
      setLoading(true);
      setError(null);
      try {
        const result = await listMembers(page, PAGE_SIZE, filters, signal);
        setData(result);
      } catch (err) {
        if (err instanceof ApiError) {
          setError(err.status ? `Failed (${err.status}): ${err.message}` : err.message);
        } else {
          setError("Failed to load members.");
        }
      } finally {
        setLoading(false);
      }
    },
    [filters, page],
  );

  useEffect(() => {
    const ctrl = new AbortController();
    void load(ctrl.signal);
    return () => ctrl.abort();
  }, [load]);

  const approve = async (m: MemberListItem) => {
    if (m.registrationStatus === RegistrationStatus.Approved) return;
    setApprovingId(m.id);
    try {
      await approveMember(m.id);
      await load();
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setApprovingId(null);
    }
  };

  const toggleMedical = async (m: MemberListItem) => {
    if (m.role !== MemberRole.Athlete) return;
    const next = !(m.medicalCleared === true);
    try {
      await recordMedicalClearance(m.id, { cleared: next });
      await load();
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const onExport = async () => {
    setExporting(true);
    try {
      await downloadCsv("members");
    } catch {
      setError("Export failed.");
    } finally {
      setExporting(false);
    }
  };

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / PAGE_SIZE)) : 1;

  return (
    <div className="space-y-5">
      <div className="flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h2 className="text-xl font-semibold text-slate-900">Members</h2>
          <p className="text-sm text-slate-500">
            {data ? `${data.totalCount} total` : "—"} · page {page} of {totalPages}
          </p>
        </div>
        <div className="flex gap-2">
          <button onClick={() => void onExport()} disabled={exporting} className="btn-secondary">
            {exporting ? "Exporting…" : "Download CSV"}
          </button>
          <button onClick={() => void load()} disabled={loading} className="btn-secondary">
            {loading ? "Refreshing…" : "Refresh"}
          </button>
        </div>
      </div>

      {/* Filter bar */}
      <div className="card p-4">
        <div className="grid gap-3 md:grid-cols-4">
          <input
            type="search"
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            placeholder="Search name, SMF ID, email, national ID…"
            className="admin-input md:col-span-2"
          />
          <select value={role} onChange={(e) => { setRole(e.target.value); setPage(1); }} className="admin-input">
            <option value="">All roles</option>
            <option value="Athlete">Athlete</option>
            <option value="Coach">Coach</option>
            <option value="Referee">Referee</option>
            <option value="ClubAdmin">Club admin</option>
            <option value="FederationStaff">Federation staff</option>
            <option value="Visitor">Visitor</option>
          </select>
          <select value={status} onChange={(e) => { setStatus(e.target.value); setPage(1); }} className="admin-input">
            <option value="">All statuses</option>
            <option value="Pending">Pending</option>
            <option value="Approved">Approved</option>
            <option value="Active">Active</option>
          </select>
        </div>
      </div>

      {error && (
        <div className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-800">
          {error}
        </div>
      )}

      {/* Table */}
      <div className="card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-sm">
            <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
              <tr>
                <th className="px-5 py-2.5">SMF ID</th>
                <th className="px-5 py-2.5">Name</th>
                <th className="px-5 py-2.5">Contact</th>
                <th className="px-5 py-2.5">Role</th>
                <th className="px-5 py-2.5">DOB</th>
                <th className="px-5 py-2.5">Readiness</th>
                <th className="px-5 py-2.5">Status</th>
                <th className="px-5 py-2.5 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {!data && loading && (
                <tr><td className="px-5 py-6 text-slate-400" colSpan={8}>Loading…</td></tr>
              )}
              {data && data.items.length === 0 && (
                <tr><td className="px-5 py-6 text-slate-400" colSpan={8}>No members match these filters.</td></tr>
              )}
              {data?.items.map((m) => (
                <tr key={m.id} className="hover:bg-slate-50/70">
                  <td className="px-5 py-2.5 font-mono text-xs text-slate-700">{m.smF_ID}</td>
                  <td className="px-5 py-2.5">
                    <Link to={`/members/${m.id}`} className="text-slate-900 hover:text-brand-700">
                      {m.fullName}
                    </Link>
                  </td>
                  <td className="px-5 py-2.5 text-slate-700">
                    <div className="text-xs">{m.email || "—"}</div>
                    <div className="text-xs text-slate-500">{m.phoneNumber || ""}</div>
                  </td>
                  <td className="px-5 py-2.5 text-slate-700">{m.role}</td>
                  <td className="px-5 py-2.5 text-slate-700">{m.dateOfBirth}</td>
                  <td className="px-5 py-2.5">
                    {m.role === "Athlete" ? <ReadinessBadge m={m} /> : <span className="text-xs text-slate-400">—</span>}
                  </td>
                  <td className="px-5 py-2.5"><StatusBadge status={m.registrationStatus} /></td>
                  <td className="px-5 py-2.5">
                    <div className="flex justify-end gap-2">
                      {m.role === "Athlete" && (
                        <button
                          onClick={() => void toggleMedical(m)}
                          className="btn-secondary !px-2.5 !py-1 text-xs"
                          title="Toggle medical clearance"
                        >
                          {m.medicalCleared === true ? "Revoke medical" : "Clear medical"}
                        </button>
                      )}
                      {m.registrationStatus === RegistrationStatus.Pending && (
                        <button
                          onClick={() => void approve(m)}
                          disabled={approvingId === m.id}
                          className="btn-primary !px-3 !py-1 text-xs"
                        >
                          {approvingId === m.id ? "Approving…" : "Approve"}
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {data && totalPages > 1 && (
          <div className="flex items-center justify-between border-t border-slate-200 bg-slate-50 px-5 py-3 text-sm">
            <span className="text-slate-500">
              Showing {(page - 1) * PAGE_SIZE + 1}–{Math.min(page * PAGE_SIZE, data.totalCount)} of {data.totalCount}
            </span>
            <div className="flex gap-2">
              <button className="btn-secondary !py-1 text-xs" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
                ← Prev
              </button>
              <button className="btn-secondary !py-1 text-xs" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
                Next →
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

function StatusBadge({ status }: { status: RegistrationStatus }) {
  const cls =
    status === RegistrationStatus.Approved
      ? "bg-emerald-50 text-emerald-700 ring-emerald-500/30"
      : status === RegistrationStatus.Active
        ? "bg-emerald-50 text-emerald-700 ring-emerald-500/30"
        : "bg-amber-50 text-amber-800 ring-amber-400/40";
  return <span className={clsx("inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset", cls)}>{status}</span>;
}

function ReadinessBadge({ m }: { m: MemberListItem }) {
  if (m.medicalCleared === true) {
    return (
      <div>
        <span className="inline-flex items-center rounded-full bg-emerald-50 px-2 py-0.5 text-xs font-medium text-emerald-700 ring-1 ring-inset ring-emerald-500/30">
          Cleared
        </span>
        {typeof m.weightCategoryKg === "number" && (
          <div className="mt-1 text-xs text-slate-500">{m.weightCategoryKg} kg</div>
        )}
      </div>
    );
  }
  if (m.medicalCleared === false) {
    return (
      <span className="inline-flex items-center rounded-full bg-rose-50 px-2 py-0.5 text-xs font-medium text-rose-700 ring-1 ring-inset ring-rose-500/30">
        Not cleared
      </span>
    );
  }
  return (
    <span className="inline-flex items-center rounded-full bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-600 ring-1 ring-inset ring-slate-300">
      Pending
    </span>
  );
}
