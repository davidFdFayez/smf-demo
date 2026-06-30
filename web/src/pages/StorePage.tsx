import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import {
  formatMoney,
  listCategories,
  listProducts,
  type CategoryDto,
  type PagedResult,
  type ProductSummaryDto,
} from "../services/storeApi";
import { useCart } from "../features/store/useCart";

export function StorePage() {
  const [categories, setCategories] = useState<CategoryDto[]>([]);
  const [activeCategory, setActiveCategory] = useState<string | undefined>(undefined);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [data, setData] = useState<PagedResult<ProductSummaryDto> | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    listCategories()
      .then((cs) => setCategories(cs.filter((c) => c.isActive)))
      .catch((err) => setError((err as Error).message));
  }, []);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    listProducts({ page, pageSize: 24, search: search.trim() || undefined, category: activeCategory })
      .then((res) => { if (!cancelled) setData(res); })
      .catch((err) => { if (!cancelled) setError((err as Error).message); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [page, search, activeCategory]);

  const visibleProducts = data?.items ?? [];

  return (
    <div className="mx-auto max-w-7xl px-4 py-10 sm:px-6">
      <header className="mb-8 flex flex-col gap-2 md:flex-row md:items-end md:justify-between">
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-smf-700">Federation store</p>
          <h1 className="mt-1 text-3xl font-bold text-slate-900">Sports equipment & apparel</h1>
          <p className="mt-2 max-w-2xl text-sm text-slate-600">
            Official kit, training gear and protective equipment, hand-picked for SMF athletes,
            coaches, and clubs. All prices include VAT.
          </p>
        </div>
        <Link
          to="/store/cart"
          className="inline-flex items-center gap-2 self-start rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
        >
          <CartIcon /> View cart <CartBadge />
        </Link>
      </header>

      <div className="mb-6 flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
        <div className="flex flex-wrap gap-2">
          <CategoryChip active={activeCategory === undefined} onClick={() => { setActiveCategory(undefined); setPage(1); }}>
            All
          </CategoryChip>
          {categories.map((c) => (
            <CategoryChip
              key={c.id}
              active={activeCategory === c.slug}
              onClick={() => { setActiveCategory(c.slug); setPage(1); }}
            >
              {c.name}
            </CategoryChip>
          ))}
        </div>
        <input
          type="search"
          value={search}
          onChange={(e) => { setSearch(e.target.value); setPage(1); }}
          placeholder="Search gloves, shorts, headguards…"
          className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm focus:border-smf-500 focus:outline-none focus:ring-1 focus:ring-smf-500 md:w-72"
        />
      </div>

      {error && (
        <div className="mb-6 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {error}
        </div>
      )}

      {loading && !data ? (
        <ProductGridSkeleton />
      ) : visibleProducts.length === 0 ? (
        <div className="rounded-2xl border border-dashed border-slate-300 bg-white p-10 text-center">
          <p className="text-sm text-slate-500">No products match those filters.</p>
        </div>
      ) : (
        <ProductGrid products={visibleProducts} />
      )}

      {data && data.totalPages > 1 && (
        <Pagination page={data.page} totalPages={data.totalPages} onChange={setPage} />
      )}
    </div>
  );
}

function ProductGrid({ products }: { products: ProductSummaryDto[] }) {
  return (
    <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
      {products.map((p) => (
        <ProductCard key={p.id} product={p} />
      ))}
    </div>
  );
}

function ProductCard({ product }: { product: ProductSummaryDto }) {
  const outOfStock = product.stockOnHand <= 0;
  return (
    <Link
      to={`/store/p/${product.id}`}
      className="group flex flex-col overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm transition hover:-translate-y-0.5 hover:border-smf-300 hover:shadow-md"
    >
      <div className="relative aspect-square overflow-hidden bg-slate-100">
        {product.imageUrl ? (
          <img
            src={product.imageUrl}
            alt={product.name}
            loading="lazy"
            className="h-full w-full object-cover transition group-hover:scale-105"
          />
        ) : (
          <Placeholder />
        )}
        {outOfStock && (
          <span className="absolute left-3 top-3 rounded-full bg-slate-900/85 px-2 py-0.5 text-[10px] font-medium uppercase tracking-wide text-white">
            Out of stock
          </span>
        )}
        {!outOfStock && product.isLowStock && (
          <span className="absolute left-3 top-3 rounded-full bg-amber-500/90 px-2 py-0.5 text-[10px] font-medium uppercase tracking-wide text-white">
            Low stock
          </span>
        )}
      </div>
      <div className="flex flex-1 flex-col gap-2 p-4">
        <span className="text-[10px] font-semibold uppercase tracking-wide text-smf-700">
          {product.categoryName}
        </span>
        <h3 className="line-clamp-2 text-sm font-semibold text-slate-900 group-hover:text-smf-700">
          {product.name}
        </h3>
        <div className="mt-auto flex items-end justify-between">
          <span className="text-base font-bold text-slate-900">
            {formatMoney(product.priceMinor, product.currency)}
          </span>
          <span className="text-xs text-slate-500">{product.sku}</span>
        </div>
      </div>
    </Link>
  );
}

function CategoryChip({
  active, onClick, children,
}: {
  active: boolean;
  onClick: () => void;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={
        "rounded-full px-3 py-1.5 text-sm font-medium transition " +
        (active
          ? "bg-smf-700 text-white"
          : "border border-slate-200 bg-white text-slate-700 hover:bg-slate-50")
      }
    >
      {children}
    </button>
  );
}

function Pagination({
  page, totalPages, onChange,
}: { page: number; totalPages: number; onChange: (p: number) => void }) {
  const pages = useMemo(() => {
    const max = Math.min(totalPages, 7);
    const half = Math.floor(max / 2);
    const start = Math.max(1, Math.min(page - half, totalPages - max + 1));
    return Array.from({ length: max }, (_, i) => start + i);
  }, [page, totalPages]);

  return (
    <nav className="mt-10 flex items-center justify-center gap-1.5">
      <PageBtn disabled={page === 1} onClick={() => onChange(page - 1)}>‹</PageBtn>
      {pages.map((n) => (
        <PageBtn key={n} active={n === page} onClick={() => onChange(n)}>{n}</PageBtn>
      ))}
      <PageBtn disabled={page === totalPages} onClick={() => onChange(page + 1)}>›</PageBtn>
    </nav>
  );
}

function PageBtn({
  active, disabled, onClick, children,
}: {
  active?: boolean;
  disabled?: boolean;
  onClick: () => void;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={onClick}
      className={
        "min-w-[2.25rem] rounded-lg border px-2.5 py-1.5 text-sm font-medium transition " +
        (active
          ? "border-smf-600 bg-smf-600 text-white"
          : "border-slate-200 bg-white text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50")
      }
    >
      {children}
    </button>
  );
}

function ProductGridSkeleton() {
  return (
    <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
      {Array.from({ length: 8 }).map((_, i) => (
        <div key={i} className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
          <div className="aspect-square animate-pulse rounded-xl bg-slate-100" />
          <div className="mt-4 h-3 w-1/3 animate-pulse rounded bg-slate-100" />
          <div className="mt-2 h-4 w-3/4 animate-pulse rounded bg-slate-100" />
          <div className="mt-3 h-5 w-1/2 animate-pulse rounded bg-slate-100" />
        </div>
      ))}
    </div>
  );
}

function CartBadge() {
  const { itemCount } = useCart();
  if (itemCount <= 0) return null;
  return (
    <span className="ml-1 inline-flex h-5 min-w-[1.25rem] items-center justify-center rounded-full bg-smf-700 px-1.5 text-[11px] font-semibold text-white">
      {itemCount}
    </span>
  );
}

function CartIcon() {
  return (
    <svg viewBox="0 0 24 24" width="16" height="16" fill="none" aria-hidden>
      <path d="M3 4h2l2.5 11.5a2 2 0 0 0 2 1.5h7a2 2 0 0 0 2-1.5L21 8H6"
        stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
      <circle cx="10" cy="20" r="1.4" fill="currentColor" />
      <circle cx="17" cy="20" r="1.4" fill="currentColor" />
    </svg>
  );
}

function Placeholder() {
  return (
    <div className="flex h-full w-full items-center justify-center text-slate-300">
      <svg viewBox="0 0 24 24" width="48" height="48" fill="none">
        <path d="M4 7h16v12H4z" stroke="currentColor" strokeWidth="1.2" />
        <path d="M4 13l4-3 5 4 3-2 4 3" stroke="currentColor" strokeWidth="1.2" strokeLinejoin="round" />
        <circle cx="9" cy="10" r="1.4" fill="currentColor" />
      </svg>
    </div>
  );
}
