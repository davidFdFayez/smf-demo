import { Link } from "react-router-dom";
import { formatMoney } from "../services/storeApi";
import { useCart } from "../features/store/useCart";

export function StoreCartPage() {
  const { cart, loading, error, update, remove, clear } = useCart();

  if (loading && !cart) {
    return (
      <div className="mx-auto max-w-5xl px-4 py-12">
        <div className="h-6 w-40 animate-pulse rounded bg-slate-100" />
        <div className="mt-6 h-32 animate-pulse rounded-2xl bg-slate-100" />
      </div>
    );
  }

  const items = cart?.items ?? [];

  return (
    <div className="mx-auto max-w-5xl px-4 py-10 sm:px-6">
      <h1 className="text-3xl font-bold text-slate-900">Your cart</h1>

      {error && (
        <div className="mt-4 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {error}
        </div>
      )}

      {items.length === 0 ? (
        <div className="mt-8 rounded-2xl border border-dashed border-slate-300 bg-white p-10 text-center">
          <p className="text-sm text-slate-500">Your cart is empty.</p>
          <Link to="/store" className="mt-4 inline-block text-sm font-medium text-smf-700">
            Continue shopping →
          </Link>
        </div>
      ) : (
        <div className="mt-8 grid gap-8 lg:grid-cols-[1fr_360px]">
          <div className="space-y-3">
            {items.map((line) => (
              <div
                key={line.productId}
                className="flex items-center gap-4 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm"
              >
                <div className="h-20 w-20 flex-shrink-0 overflow-hidden rounded-lg bg-slate-100">
                  {line.imageUrl ? (
                    <img src={line.imageUrl} alt={line.productName} className="h-full w-full object-cover" />
                  ) : null}
                </div>
                <div className="min-w-0 flex-1">
                  <Link
                    to={`/store/p/${line.productId}`}
                    className="line-clamp-1 text-sm font-semibold text-slate-900 hover:text-smf-700"
                  >
                    {line.productName}
                  </Link>
                  <div className="text-xs text-slate-500">SKU {line.productSku}</div>
                  <div className="mt-1 text-sm text-slate-700">
                    {formatMoney(line.unitPriceMinor, cart?.currency)} each
                  </div>
                </div>

                <div className="inline-flex items-center rounded-lg border border-slate-300">
                  <button
                    type="button"
                    onClick={() => update(line.productId, Math.max(0, line.quantity - 1))}
                    className="px-2.5 py-1.5 text-slate-700"
                    aria-label="Decrease"
                  >−</button>
                  <span className="min-w-[2rem] text-center text-sm font-semibold">
                    {line.quantity}
                  </span>
                  <button
                    type="button"
                    onClick={() => update(line.productId, Math.min(50, line.quantity + 1))}
                    className="px-2.5 py-1.5 text-slate-700"
                    aria-label="Increase"
                  >+</button>
                </div>

                <div className="w-28 text-right text-sm font-semibold text-slate-900">
                  {formatMoney(line.lineTotalMinor, cart?.currency)}
                </div>

                <button
                  type="button"
                  onClick={() => remove(line.productId)}
                  className="rounded-md p-1.5 text-slate-400 hover:bg-slate-100 hover:text-red-600"
                  aria-label="Remove"
                >
                  <svg viewBox="0 0 24 24" width="18" height="18" fill="none">
                    <path d="M5 7h14M10 11v6M14 11v6M6 7l1 13a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2l1-13M9 7V5a2 2 0 0 1 2-2h2a2 2 0 0 1 2 2v2"
                      stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                  </svg>
                </button>
              </div>
            ))}

            <div className="flex justify-between pt-4">
              <Link to="/store" className="text-sm font-medium text-smf-700 hover:underline">
                ← Continue shopping
              </Link>
              <button
                type="button"
                onClick={() => clear()}
                className="text-sm text-slate-500 hover:text-red-600"
              >
                Clear cart
              </button>
            </div>
          </div>

          <aside className="h-fit rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
            <h2 className="text-base font-semibold text-slate-900">Order summary</h2>
            <dl className="mt-4 space-y-2 text-sm">
              <div className="flex justify-between">
                <dt className="text-slate-600">Subtotal</dt>
                <dd className="font-medium text-slate-900">
                  {formatMoney(cart?.subtotalMinor ?? 0, cart?.currency)}
                </dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-slate-600">
                  VAT ({((cart?.vatRateBp ?? 0) / 100).toFixed(0)}%)
                </dt>
                <dd className="font-medium text-slate-900">
                  {formatMoney(cart?.estimatedVatMinor ?? 0, cart?.currency)}
                </dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-slate-600">Shipping</dt>
                <dd className="text-xs text-slate-500">Calculated at checkout</dd>
              </div>
              <div className="mt-3 border-t border-slate-200 pt-3 flex justify-between text-base font-semibold">
                <dt>Estimated total</dt>
                <dd className="text-smf-700">
                  {formatMoney(cart?.estimatedTotalMinor ?? 0, cart?.currency)}
                </dd>
              </div>
            </dl>

            <Link
              to="/store/checkout"
              className="mt-6 inline-flex w-full items-center justify-center rounded-lg bg-smf-700 px-4 py-3 text-sm font-semibold text-white transition hover:bg-smf-600"
            >
              Proceed to checkout
            </Link>
            <p className="mt-3 text-center text-xs text-slate-500">
              Mada · Apple Pay · Visa · Mastercard
            </p>
          </aside>
        </div>
      )}
    </div>
  );
}
