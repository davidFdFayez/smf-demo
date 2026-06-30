import { useEffect, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import {
  formatMoney,
  getOrder,
  invoiceDocumentUrl,
  type OrderDto,
} from "../services/storeApi";

const STATUS_STYLE: Record<string, string> = {
  AwaitingPayment: "bg-amber-50 text-amber-800 border-amber-200",
  Paid: "bg-emerald-50 text-emerald-800 border-emerald-200",
  Fulfilled: "bg-emerald-100 text-emerald-900 border-emerald-300",
  Cancelled: "bg-slate-100 text-slate-700 border-slate-300",
  Refunded: "bg-blue-50 text-blue-800 border-blue-200",
};

export function StoreOrderPage() {
  const { id } = useParams<{ id: string }>();
  const [params] = useSearchParams();
  const paymentHint = params.get("payment");

  const [order, setOrder] = useState<OrderDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    let cancelled = false;
    let attempts = 0;

    const tick = async () => {
      try {
        const next = await getOrder(id);
        if (cancelled) return;
        setOrder(next);
        setLoading(false);
        // The webhook may take a beat to flip Awaiting → Paid; poll lightly.
        if (next.status === "AwaitingPayment" && attempts < 6) {
          attempts++;
          window.setTimeout(tick, 1500);
        }
      } catch (err) {
        if (!cancelled) {
          setError((err as Error).message);
          setLoading(false);
        }
      }
    };

    tick();
    return () => { cancelled = true; };
  }, [id]);

  if (loading && !order) {
    return (
      <div className="mx-auto max-w-3xl px-4 py-12">
        <div className="h-7 w-48 animate-pulse rounded bg-slate-100" />
        <div className="mt-6 h-32 animate-pulse rounded-2xl bg-slate-100" />
      </div>
    );
  }

  if (error || !order) {
    return (
      <div className="mx-auto max-w-3xl px-4 py-12">
        <div className="rounded-2xl border border-red-200 bg-red-50 p-6 text-sm text-red-700">
          {error ?? "Order not found."}
        </div>
        <Link to="/store" className="mt-4 inline-block text-sm font-medium text-smf-700">
          ← Back to store
        </Link>
      </div>
    );
  }

  const isPaid = order.status === "Paid" || order.status === "Fulfilled";

  return (
    <div className="mx-auto max-w-3xl px-4 py-10 sm:px-6">
      <header className="mb-6 flex items-start justify-between gap-4">
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-smf-700">Order</p>
          <h1 className="mt-1 text-2xl font-bold text-slate-900">{order.orderNumber}</h1>
          <p className="mt-1 text-sm text-slate-500">
            Placed {new Date(order.createdAtUtc).toLocaleString()}
          </p>
        </div>
        <span
          className={
            "inline-flex items-center rounded-full border px-3 py-1 text-xs font-semibold " +
            (STATUS_STYLE[order.status] ?? "border-slate-200 bg-white text-slate-700")
          }
        >
          {humaniseStatus(order.status)}
        </span>
      </header>

      {!isPaid && order.status === "AwaitingPayment" && (
        <div className="mb-6 rounded-2xl border border-amber-200 bg-amber-50 p-5 text-sm text-amber-900">
          Waiting for the payment confirmation from the gateway.
          {paymentHint ? ` Payment ID ${paymentHint}.` : ""} This page will refresh automatically.
        </div>
      )}

      {isPaid && (
        <div className="mb-6 rounded-2xl border border-emerald-200 bg-emerald-50 p-5 text-sm text-emerald-900">
          Payment received — thank you! A receipt has been generated below.
        </div>
      )}

      <section className="rounded-2xl border border-slate-200 bg-white shadow-sm">
        <div className="border-b border-slate-200 p-5">
          <h2 className="text-sm font-semibold text-slate-900">Items</h2>
        </div>
        <ul className="divide-y divide-slate-200">
          {order.items.map((line) => (
            <li key={line.productId} className="flex items-center justify-between gap-4 p-5">
              <div className="min-w-0">
                <div className="line-clamp-1 text-sm font-medium text-slate-900">
                  {line.productName}
                </div>
                <div className="text-xs text-slate-500">
                  SKU {line.productSku} · Qty {line.quantity}
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
            <dd className="text-smf-700">{formatMoney(order.totalMinor, order.currency)}</dd>
          </div>
        </dl>
      </section>

      <section className="mt-6 grid gap-4 md:grid-cols-2">
        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
          <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-500">Buyer</h3>
          <p className="mt-2 text-sm font-medium text-slate-900">{order.buyerName}</p>
          <p className="text-xs text-slate-500">{order.buyerEmail}</p>
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
      </section>

      {isPaid && order.invoiceId && (
        <section className="mt-6 flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
          <div>
            <h3 className="text-sm font-semibold text-slate-900">Tax invoice ready</h3>
            <p className="text-xs text-slate-500">
              VAT-compliant invoice keyed to payment {order.paymentId?.slice(0, 8)}…
            </p>
          </div>
          <div className="flex items-center gap-2">
            <a
              href={invoiceDocumentUrl(order.invoiceId, "html")}
              target="_blank"
              rel="noreferrer"
              className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm font-medium text-slate-800 hover:bg-slate-50"
            >
              View HTML
            </a>
            <a
              href={invoiceDocumentUrl(order.invoiceId, "pdf")}
              target="_blank"
              rel="noreferrer"
              className="rounded-lg bg-smf-700 px-3 py-2 text-sm font-semibold text-white hover:bg-smf-600"
            >
              Download PDF
            </a>
          </div>
        </section>
      )}

      <div className="mt-8 text-center">
        <Link to="/store" className="text-sm font-medium text-smf-700 hover:underline">
          Continue shopping →
        </Link>
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

function humaniseStatus(s: string) {
  switch (s) {
    case "AwaitingPayment": return "Awaiting payment";
    case "Paid": return "Paid";
    case "Fulfilled": return "Fulfilled";
    case "Cancelled": return "Cancelled";
    case "Refunded": return "Refunded";
    default: return s;
  }
}
