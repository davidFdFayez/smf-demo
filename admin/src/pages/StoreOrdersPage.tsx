import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import {
  formatMoney,
  listOrders,
  type OrderListItem,
  type OrderStatus,
  type PagedResult,
} from "../services/storeApi";

const STATUSES: OrderStatus[] = [
  "AwaitingPayment", "Paid", "Fulfilled", "Cancelled", "Refunded",
];

const STATUS_STYLE: Record<OrderStatus, string> = {
  AwaitingPayment: "bg-amber-50 text-amber-800 border-amber-200",
  Paid: "bg-emerald-50 text-emerald-800 border-emerald-200",
  Fulfilled: "bg-emerald-100 text-emerald-900 border-emerald-300",
  Cancelled: "bg-slate-100 text-slate-700 border-slate-300",
  Refunded: "bg-blue-50 text-blue-800 border-blue-200",
};

export function StoreOrdersPage() {
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState<OrderStatus | "">("");
  const [data, setData] = useState<PagedResult<OrderListItem> | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const reload = useCallback(async () => {
    setLoading(true); setError(null);
    try {
      const res = await listOrders({
        page, pageSize: 25,
        search: search.trim() || undefined,
        status: status || undefined,
      });
      setData(res);
    } catch (err) { setError((err as Error).message); }
    finally { setLoading(false); }
  }, [page, search, status]);

  useEffect(() => { reload(); }, [reload]);

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-2xl font-bold text-slate-900">Store orders</h1>
        <p className="mt-1 text-sm text-slate-500">
          Review e-commerce orders, fulfil paid ones, or cancel and release stock.
        </p>
      </header>

      <section className="rounded-2xl border border-slate-200 bg-white shadow-sm">
        <div className="flex flex-col gap-3 border-b border-slate-200 p-4 md:flex-row md:items-center md:justify-between">
          <div className="flex flex-wrap items-center gap-2">
            <select
              value={status}
              onChange={(e) => { setStatus(e.target.value as OrderStatus | ""); setPage(1); }}
              className="rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-sm"
            >
              <option value="">All statuses</option>
              {STATUSES.map((s) => <option key={s} value={s}>{humanise(s)}</option>)}
            </select>
            <input
              type="search"
              value={search}
              onChange={(e) => { setSearch(e.target.value); setPage(1); }}
              placeholder="Order #, buyer name, email…"
              className="rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-sm"
            />
          </div>
          <button
            type="button"
            onClick={() => reload()}
            className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-50"
          >
            Refresh
          </button>
        </div>

        {error && (
          <div className="border-b border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>
        )}

        <div className="overflow-x-auto">
          <table className="min-w-full text-sm">
            <thead className="bg-slate-50 text-xs font-semibold uppercase tracking-wide text-slate-500">
              <tr>
                <th className="px-4 py-3 text-left">Order #</th>
                <th className="px-4 py-3 text-left">Buyer</th>
                <th className="px-4 py-3 text-right">Total</th>
                <th className="px-4 py-3 text-right">Items</th>
                <th className="px-4 py-3 text-left">Status</th>
                <th className="px-4 py-3 text-left">Created</th>
                <th className="px-4 py-3 text-right"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-200">
              {loading && !data && (
                <tr><td colSpan={7} className="px-4 py-8 text-center text-sm text-slate-500">Loading…</td></tr>
              )}
              {data?.items.map((o) => (
                <tr key={o.id} className="hover:bg-slate-50">
                  <td className="px-4 py-3 font-mono text-xs text-slate-700">{o.orderNumber}</td>
                  <td className="px-4 py-3">
                    <div className="font-medium text-slate-900">{o.buyerName}</div>
                    <div className="text-xs text-slate-500">{o.buyerEmail}</div>
                  </td>
                  <td className="px-4 py-3 text-right font-semibold text-slate-900">
                    {formatMoney(o.totalMinor, o.currency)}
                  </td>
                  <td className="px-4 py-3 text-right text-slate-600">{o.itemCount}</td>
                  <td className="px-4 py-3">
                    <span className={`inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-semibold ${STATUS_STYLE[o.status]}`}>
                      {humanise(o.status)}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-slate-600">
                    {new Date(o.createdAtUtc).toLocaleString()}
                  </td>
                  <td className="px-4 py-3 text-right">
                    <Link
                      to={`/store/orders/${o.id}`}
                      className="rounded-md border border-slate-200 bg-white px-2 py-1 text-xs font-semibold text-slate-700 hover:bg-slate-50"
                    >
                      Open
                    </Link>
                  </td>
                </tr>
              ))}
              {data && data.items.length === 0 && (
                <tr><td colSpan={7} className="px-4 py-8 text-center text-sm text-slate-500">No orders match those filters.</td></tr>
              )}
            </tbody>
          </table>
        </div>

        {data && data.totalPages > 1 && (
          <div className="flex items-center justify-between border-t border-slate-200 px-4 py-3 text-sm">
            <span className="text-slate-500">
              Page {data.page} of {data.totalPages} · {data.totalCount} orders
            </span>
            <div className="flex items-center gap-2">
              <button
                type="button"
                disabled={page <= 1}
                onClick={() => setPage((p) => p - 1)}
                className="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-sm font-medium hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
              >Prev</button>
              <button
                type="button"
                disabled={page >= data.totalPages}
                onClick={() => setPage((p) => p + 1)}
                className="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-sm font-medium hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
              >Next</button>
            </div>
          </div>
        )}
      </section>
    </div>
  );
}

function humanise(s: OrderStatus) {
  switch (s) {
    case "AwaitingPayment": return "Awaiting payment";
    case "Paid": return "Paid";
    case "Fulfilled": return "Fulfilled";
    case "Cancelled": return "Cancelled";
    case "Refunded": return "Refunded";
  }
}
