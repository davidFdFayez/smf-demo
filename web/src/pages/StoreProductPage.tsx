import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import {
  formatMoney,
  getProduct,
  type ProductDetailDto,
} from "../services/storeApi";
import { useCart } from "../features/store/useCart";

export function StoreProductPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { add, loading: cartBusy } = useCart();

  const [product, setProduct] = useState<ProductDetailDto | null>(null);
  const [qty, setQty] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [adding, setAdding] = useState(false);
  const [added, setAdded] = useState(false);

  useEffect(() => {
    if (!id) return;
    let cancelled = false;
    setLoading(true);
    setError(null);
    getProduct(id)
      .then((p) => { if (!cancelled) setProduct(p); })
      .catch((err) => { if (!cancelled) setError((err as Error).message); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [id]);

  const onAdd = async () => {
    if (!product) return;
    setAdding(true);
    try {
      await add(product.id, qty);
      setAdded(true);
      setTimeout(() => setAdded(false), 1800);
    } finally {
      setAdding(false);
    }
  };

  const buyNow = async () => {
    if (!product) return;
    setAdding(true);
    try {
      await add(product.id, qty);
      navigate("/store/checkout");
    } finally {
      setAdding(false);
    }
  };

  if (loading) {
    return (
      <div className="mx-auto grid max-w-6xl gap-10 px-4 py-12 md:grid-cols-2">
        <div className="aspect-square animate-pulse rounded-2xl bg-slate-100" />
        <div className="space-y-3">
          <div className="h-3 w-24 animate-pulse rounded bg-slate-100" />
          <div className="h-7 w-3/4 animate-pulse rounded bg-slate-100" />
          <div className="h-5 w-1/3 animate-pulse rounded bg-slate-100" />
          <div className="mt-6 h-20 animate-pulse rounded bg-slate-100" />
        </div>
      </div>
    );
  }

  if (error || !product) {
    return (
      <div className="mx-auto max-w-6xl px-4 py-12">
        <div className="rounded-2xl border border-red-200 bg-red-50 p-6 text-sm text-red-700">
          {error ?? "Product not found."}
        </div>
        <Link to="/store" className="mt-4 inline-block text-sm font-medium text-smf-700">
          ← Back to store
        </Link>
      </div>
    );
  }

  const outOfStock = product.stockOnHand <= 0;

  return (
    <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6">
      <div className="mb-6 text-sm">
        <Link to="/store" className="text-slate-500 hover:text-smf-700">Store</Link>
        <span className="mx-2 text-slate-300">/</span>
        <span className="text-slate-700">{product.categoryName}</span>
      </div>

      <div className="grid gap-10 md:grid-cols-2">
        <div className="overflow-hidden rounded-2xl border border-slate-200 bg-slate-50">
          <div className="aspect-square">
            {product.imageUrl ? (
              <img src={product.imageUrl} alt={product.name} className="h-full w-full object-cover" />
            ) : (
              <div className="flex h-full w-full items-center justify-center text-slate-300">
                <svg viewBox="0 0 24 24" width="80" height="80" fill="none">
                  <path d="M4 7h16v12H4z" stroke="currentColor" strokeWidth="1.2" />
                  <path d="M4 13l4-3 5 4 3-2 4 3" stroke="currentColor" strokeWidth="1.2" />
                </svg>
              </div>
            )}
          </div>
        </div>

        <div className="flex flex-col">
          <span className="text-xs font-semibold uppercase tracking-wide text-smf-700">
            {product.categoryName}
          </span>
          <h1 className="mt-2 text-3xl font-bold text-slate-900">{product.name}</h1>
          <div className="mt-1 text-sm text-slate-500">SKU {product.sku}</div>

          <div className="mt-5 text-3xl font-extrabold text-slate-900">
            {formatMoney(product.priceMinor, product.currency)}
          </div>

          <div className="mt-2 text-sm">
            {outOfStock ? (
              <span className="inline-flex items-center gap-1.5 rounded-full bg-red-50 px-2.5 py-1 text-red-700">
                <Dot className="bg-red-500" /> Out of stock
              </span>
            ) : product.isLowStock ? (
              <span className="inline-flex items-center gap-1.5 rounded-full bg-amber-50 px-2.5 py-1 text-amber-700">
                <Dot className="bg-amber-500" /> Only {product.stockOnHand} left
              </span>
            ) : (
              <span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-50 px-2.5 py-1 text-emerald-700">
                <Dot className="bg-emerald-500" /> In stock
              </span>
            )}
          </div>

          {product.description && (
            <div className="mt-6 whitespace-pre-line text-sm leading-relaxed text-slate-700">
              {product.description}
            </div>
          )}

          <div className="mt-8 flex items-center gap-3">
            <QtyStepper
              value={qty}
              max={Math.min(product.stockOnHand, 50)}
              disabled={outOfStock}
              onChange={setQty}
            />
            <button
              type="button"
              onClick={onAdd}
              disabled={outOfStock || adding || cartBusy}
              className="inline-flex flex-1 items-center justify-center gap-2 rounded-lg border border-slate-300 bg-white px-4 py-2.5 text-sm font-semibold text-slate-800 transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
            >
              {added ? "Added ✓" : adding ? "Adding…" : "Add to cart"}
            </button>
            <button
              type="button"
              onClick={buyNow}
              disabled={outOfStock || adding || cartBusy}
              className="inline-flex flex-1 items-center justify-center gap-2 rounded-lg bg-smf-700 px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-smf-600 disabled:cursor-not-allowed disabled:opacity-50"
            >
              Buy now
            </button>
          </div>

          <p className="mt-4 text-xs text-slate-500">
            Secure checkout via Tap Payments. Mada and Apple Pay supported.
          </p>
        </div>
      </div>
    </div>
  );
}

function QtyStepper({
  value, onChange, max, disabled,
}: {
  value: number;
  onChange: (n: number) => void;
  max: number;
  disabled?: boolean;
}) {
  return (
    <div className="inline-flex items-center rounded-lg border border-slate-300">
      <button
        type="button"
        disabled={disabled || value <= 1}
        onClick={() => onChange(Math.max(1, value - 1))}
        className="px-2.5 py-2 text-slate-700 disabled:opacity-40"
        aria-label="Decrease"
      >−</button>
      <span className="min-w-[2.25rem] text-center text-sm font-semibold">{value}</span>
      <button
        type="button"
        disabled={disabled || value >= max}
        onClick={() => onChange(Math.min(max, value + 1))}
        className="px-2.5 py-2 text-slate-700 disabled:opacity-40"
        aria-label="Increase"
      >+</button>
    </div>
  );
}

function Dot({ className = "" }: { className?: string }) {
  return <span className={`h-1.5 w-1.5 rounded-full ${className}`} />;
}
