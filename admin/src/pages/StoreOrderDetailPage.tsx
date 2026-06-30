import { useCallback, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import {
  cancelOrder,
  formatMoney,
  fulfillOrder,
  getOrder,
  invoiceDocumentUrl,
  type OrderDto,
  type OrderStatus,
} from "../services/storeApi";

const STATUS_STYLE: Record<OrderStatus, string> = {
  AwaitingPayment: "bg-amber-50 text-amber-800 border-amber-200",
  Paid: "bg-emerald-50 text-emerald-800 border-emerald-200",
  Fulfilled: "bg-emerald-100 text-emerald-900 border-emerald-300",
  Cancelled: "bg-slate-100 text-slate-700 border-slate-300",
  Refunded: "bg-blue-50 text-blue-800 border-blue-200",
};

export function StoreOrderDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [order, setOrder] = useState<OrderDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<"fulfill" | "cancel" | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const reload = useCallback(async () => {
    if (!id) return;
    setLoading(true); setError(null);
    try {
      setOrder(await getOrder(id));
    } catch (err) { setError((err as Error).message); }
    finally { setLoading(false); }
  }, [id]);

  useEffect(() => { reload(); }, [reload]);

  if (loading && !order) {
    return <div className="text-sm text-slate-500">Loading order…</div>;
  }

  if (!order) {
    return (
      <div>
        <Link to="/store/orders" className="text-sm font-medium text-brand-700">← All orders</Link>
        <div className="mt-4 rounded-2xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          {error ?? "Order not found."}
        </div>
      </div>
    );
  }

  const onFulfill = async () => {
    setBusy("fulfill"); setError(null); setSuccess(null);
    try {
      await fulfillOrder(order.id);
      setSuccess(`Order ${order.orderNumber} marked as fulfilled.`);
      await reload();
    } catch (err) { setError((err as Error).message); }
    finally { setBusy(null); }
  };

  const onCancel = async () => {
    const reason = window.prompt("Cancellation reason (optional):") ?? undefined;
    setBusy("cancel"); setError(null); setSuccess(null);
    try {
      await cancelOrder(order.id, reason || undefined);
      setSuccess(`Order ${order.orderNumber} cancelled — stock released.`);
      await reload();
    } catch (err) { setError((err as Error).message); }
    finally { setBusy(null); }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <Link to="/store/orders" className="text-sm font-medium text-brand-700">← All orders</Link>
        <span className={`inline-flex items-center rounded-full border px-3 py-1 text-xs font-semibold ${STATUS_STYLE[order.status]}`}>
          {order.status}
        </span>
      </div>

      <header className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <h1 className="text-2xl font-bold text-slate-900">{order.orderNumber}</h1>
        <p className="mt-1 text-sm text-slate-500">
          Placed {new Date(order.createdAtUtc).toLocaleString()}
          {order.paidAtUtc ? ` · Paid ${new Date(order.paidAtUtc).toLocaleString()}` : ""}
        </p>
      </header>

      {error && <Banner kind="error" message={error} />}
      {success && <Banner kind="success" message={success} />}

      <div className="grid gap-6 lg:grid-cols-[1fr_300px]">
        <section className="rounded-2xl border border-slate-200 bg-white shadow-sm">
          <div className="border-b border-slate-200 p-5">
            <h2 className="text-sm font-semibold text-slate-900">Items</h2>
          </div>
          <ul className="divide-y divide-slate-200">
            {order.items.map((line) => (
              <li key={line.productId} className="flex items-center justify-between gap-4 p-5">
                <div className="min-w-0">
                  <Link
                    to={`/store/products?focus=${line.productId}`}
                    className="line-clamp-1 text-sm font-medium text-slate-900 hover:text-brand-700"
                  >
                    {line.productName}
                  </Link>
                  <div className="text-xs text-slate-500">
                    SKU {line.productSku} · Qty {line.quantity} · {formatMoney(line.unitPriceMinor, order.currency)} each
                  </div>
                </div>
                <div className="text-sm font-semibold text-slate-900">
                  {formatMoney(line.lineTotalMinor, order.currency)}
                </div>
              </li>
            ))}
          </ul>
          <dl className="space-y-1 border-t border-slate-200 p-5 text-sm">
            <Row label="Subtotal" value={formatMoney(order.subtotalMinor, order.currency)} />
            <Row label={`VAT (${(order.vatRateBp / 100).toFixed(0)}%)`} value={formatMoney(order.vatAmountMinor, order.currency)} />
            <Row label="Shipping" value={formatMoney(order.shippingFeeMinor, order.currency)} />
            <div className="mt-2 flex justify-between border-t border-slate-200 pt-2 text-base font-bold text-slate-900">
              <dt>Total</dt>
              <dd className="text-brand-700">{formatMoney(order.totalMinor, order.currency)}</dd>
            </div>
          </dl>
        </section>

        <aside className="space-y-4">
          <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-500">Buyer</h3>
            <p className="mt-2 text-sm font-medium text-slate-900">{order.buyerName}</p>
            <p className="text-xs text-slate-500">{order.buyerEmail}</p>
            {order.memberId && (
              <Link
                to={`/members/${order.memberId}`}
                className="mt-2 inline-block text-xs font-medium text-brand-700 hover:underline"
              >
                View member ↗
              </Link>
            )}
          </div>

          <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-500">Ship to</h3>
            <p className="mt-2 text-sm text-slate-900">
              {order.shippingAddress.recipientName}<br />
              {order.shippingAddress.line1}
              {order.shippingAddress.line2 ? <><br />{order.shippingAddress.line2}</> : null}
              <br />
              {order.shippingAddress.city}, {order.shippingAddress.region} {order.shippingAddress.postalCode}
              <br />
              {order.shippingAddress.country} · {order.shippingAddress.phoneNumber}
            </p>
          </div>

          <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-500">References</h3>
            <p className="mt-2 break-all text-xs text-slate-700">
              <span className="font-medium text-slate-500">Payment:</span> {order.paymentId ?? "—"}
            </p>
            <p className="mt-1 break-all text-xs text-slate-700">
              <span className="font-medium text-slate-500">Invoice:</span> {order.invoiceId ?? "—"}
            </p>
            {order.invoiceId && (
              <div className="mt-3 flex flex-wrap gap-2">
                <a
                  href={invoiceDocumentUrl(order.invoiceId, "html")}
                  target="_blank" rel="noreferrer"
                  className="rounded-md border border-slate-200 bg-white px-2.5 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50"
                >View HTML</a>
                <a
                  href={invoiceDocumentUrl(order.invoiceId, "pdf")}
                  target="_blank" rel="noreferrer"
                  className="rounded-md bg-brand-600 px-2.5 py-1.5 text-xs font-semibold text-white hover:bg-brand-700"
                >Download PDF</a>
              </div>
            )}
          </div>

          <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-500">Actions</h3>
            <div className="mt-3 flex flex-col gap-2">
              <button
                type="button"
                disabled={order.status !== "Paid" || busy === "fulfill"}
                onClick={onFulfill}
                className="rounded-md bg-emerald-600 px-3 py-2 text-xs font-semibold text-white hover:bg-emerald-700 disabled:cursor-not-allowed disabled:opacity-50"
              >
                {busy === "fulfill" ? "Marking…" : "Mark as fulfilled"}
              </button>
              <button
                type="button"
                disabled={
                  order.status === "Cancelled" ||
                  order.status === "Refunded" ||
                  order.status === "Fulfilled" ||
                  busy === "cancel"
                }
                onClick={onCancel}
                className="rounded-md border border-red-200 bg-white px-3 py-2 text-xs font-semibold text-red-700 hover:bg-red-50 disabled:cursor-not-allowed disabled:opacity-50"
              >
                {busy === "cancel" ? "Cancelling…" : "Cancel & release stock"}
              </button>
            </div>
          </div>
        </aside>
      </div>
    </div>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex justify-between">
      <dt className="text-slate-600">{label}</dt>
      <dd className="font-medium text-slate-900">{value}</dd>
    </div>
  );
}

function Banner({ kind, message }: { kind: "error" | "success"; message: string }) {
  const cls = kind === "error"
    ? "border-red-200 bg-red-50 text-red-700"
    : "border-emerald-200 bg-emerald-50 text-emerald-700";
  return <div className={`rounded-lg border px-4 py-3 text-sm ${cls}`}>{message}</div>;
}
