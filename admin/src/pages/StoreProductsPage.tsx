import { useCallback, useEffect, useState } from "react";
import {
  adjustStock,
  createCategory,
  createProduct,
  formatMoney,
  listCategories,
  listProducts,
  updateProduct,
  type CategoryDto,
  type CreateCategoryInput,
  type CreateProductInput,
  type PagedResult,
  type ProductSummaryDto,
  type UpdateProductInput,
} from "../services/storeApi";

interface ProductDraft {
  id?: string;
  categoryId: string;
  sku: string;
  name: string;
  description: string;
  imageUrl: string;
  priceMinor: string;
  initialStock: string;
  lowStockThreshold: string;
  isActive: boolean;
}

const EMPTY_DRAFT: ProductDraft = {
  categoryId: "",
  sku: "",
  name: "",
  description: "",
  imageUrl: "",
  priceMinor: "",
  initialStock: "",
  lowStockThreshold: "",
  isActive: true,
};

export function StoreProductsPage() {
  const [categories, setCategories] = useState<CategoryDto[]>([]);
  const [products, setProducts] = useState<PagedResult<ProductSummaryDto> | null>(null);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [filterCategory, setFilterCategory] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const [draft, setDraft] = useState<ProductDraft>(EMPTY_DRAFT);
  const [showCategory, setShowCategory] = useState(false);

  const reload = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [cats, prods] = await Promise.all([
        listCategories(),
        listProducts({
          page, pageSize: 25,
          search: search.trim() || undefined,
          category: filterCategory || undefined,
        }),
      ]);
      setCategories(cats);
      setProducts(prods);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  }, [page, search, filterCategory]);

  useEffect(() => { reload(); }, [reload]);

  const editing = !!draft.id;

  const onEdit = (p: ProductSummaryDto) => {
    setDraft({
      id: p.id,
      categoryId: p.categoryId,
      sku: p.sku,
      name: p.name,
      description: "",
      imageUrl: p.imageUrl ?? "",
      priceMinor: String(p.priceMinor),
      initialStock: String(p.stockOnHand),
      lowStockThreshold: "",
      isActive: p.isActive,
    });
    window.scrollTo({ top: 0, behavior: "smooth" });
  };

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSuccess(null);
    try {
      const priceMinor = parseInt(draft.priceMinor, 10);
      if (Number.isNaN(priceMinor) || priceMinor <= 0) {
        throw new Error("Price (in halalat) must be a positive integer.");
      }

      if (editing && draft.id) {
        const update: UpdateProductInput = {
          categoryId: draft.categoryId,
          name: draft.name.trim(),
          description: draft.description.trim() || null,
          imageUrl: draft.imageUrl.trim() || null,
          priceMinor,
          lowStockThreshold: draft.lowStockThreshold
            ? parseInt(draft.lowStockThreshold, 10)
            : null,
          isActive: draft.isActive,
        };
        await updateProduct(draft.id, update);
        setSuccess(`Updated ${draft.name}.`);
      } else {
        const initialStock = parseInt(draft.initialStock, 10);
        if (Number.isNaN(initialStock) || initialStock < 0) {
          throw new Error("Initial stock must be zero or a positive integer.");
        }
        const create: CreateProductInput = {
          categoryId: draft.categoryId,
          sku: draft.sku.trim(),
          name: draft.name.trim(),
          description: draft.description.trim() || undefined,
          imageUrl: draft.imageUrl.trim() || undefined,
          priceMinor,
          initialStock,
          lowStockThreshold: draft.lowStockThreshold
            ? parseInt(draft.lowStockThreshold, 10)
            : undefined,
        };
        await createProduct(create);
        setSuccess(`Created ${draft.name}.`);
      }
      setDraft(EMPTY_DRAFT);
      await reload();
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const onAdjust = async (id: string, delta: number) => {
    setError(null);
    try {
      await adjustStock(id, delta, "Operator adjustment");
      await reload();
    } catch (err) {
      setError((err as Error).message);
    }
  };

  return (
    <div className="space-y-6">
      <header className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <h1 className="text-2xl font-bold text-slate-900">Store catalog</h1>
          <p className="mt-1 text-sm text-slate-500">
            Maintain SKUs, prices, stock, and visibility for the federation shop.
          </p>
        </div>
        <button
          type="button"
          onClick={() => setShowCategory((s) => !s)}
          className="inline-flex items-center gap-2 self-start rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm font-medium text-slate-700 shadow-sm hover:bg-slate-50"
        >
          {showCategory ? "Hide category form" : "+ New category"}
        </button>
      </header>

      {showCategory && (
        <CategoryForm onCreated={async () => { setShowCategory(false); await reload(); }} />
      )}

      {error && <Banner kind="error" message={error} />}
      {success && <Banner kind="success" message={success} />}

      <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <h2 className="text-lg font-semibold text-slate-900">
          {editing ? "Edit product" : "New product"}
        </h2>
        <form onSubmit={onSubmit} className="mt-4 grid gap-4 lg:grid-cols-2">
          <Field label="Category">
            <select
              required
              value={draft.categoryId}
              onChange={(e) => setDraft((d) => ({ ...d, categoryId: e.target.value }))}
              className={inputCls}
            >
              <option value="">— Select —</option>
              {categories.map((c) => (
                <option key={c.id} value={c.id}>{c.name}{!c.isActive ? " (inactive)" : ""}</option>
              ))}
            </select>
          </Field>
          <Field label="SKU">
            <input
              required
              maxLength={64}
              disabled={editing}
              value={draft.sku}
              onChange={(e) => setDraft((d) => ({ ...d, sku: e.target.value }))}
              className={inputCls}
              placeholder="GLOVE-12OZ-RED"
            />
          </Field>
          <Field label="Name" colSpan={2}>
            <input
              required
              maxLength={200}
              value={draft.name}
              onChange={(e) => setDraft((d) => ({ ...d, name: e.target.value }))}
              className={inputCls}
            />
          </Field>
          <Field label="Description" colSpan={2}>
            <textarea
              rows={3}
              maxLength={4000}
              value={draft.description}
              onChange={(e) => setDraft((d) => ({ ...d, description: e.target.value }))}
              className={inputCls}
            />
          </Field>
          <Field label="Image URL" colSpan={2}>
            <input
              maxLength={1024}
              value={draft.imageUrl}
              onChange={(e) => setDraft((d) => ({ ...d, imageUrl: e.target.value }))}
              className={inputCls}
              placeholder="https://…"
            />
          </Field>
          <Field label="Price (halalat — 1 SAR = 100)">
            <input
              required
              type="number"
              min={1}
              value={draft.priceMinor}
              onChange={(e) => setDraft((d) => ({ ...d, priceMinor: e.target.value }))}
              className={inputCls}
              placeholder="29900"
            />
          </Field>
          <Field label="Low-stock threshold (optional)">
            <input
              type="number"
              min={0}
              value={draft.lowStockThreshold}
              onChange={(e) => setDraft((d) => ({ ...d, lowStockThreshold: e.target.value }))}
              className={inputCls}
              placeholder="5"
            />
          </Field>
          {!editing && (
            <Field label="Initial stock">
              <input
                required
                type="number"
                min={0}
                value={draft.initialStock}
                onChange={(e) => setDraft((d) => ({ ...d, initialStock: e.target.value }))}
                className={inputCls}
              />
            </Field>
          )}
          <Field label="Status">
            <label className="inline-flex items-center gap-2">
              <input
                type="checkbox"
                checked={draft.isActive}
                onChange={(e) => setDraft((d) => ({ ...d, isActive: e.target.checked }))}
              />
              <span className="text-sm">Active (visible on public store)</span>
            </label>
          </Field>

          <div className="flex items-center justify-end gap-3 lg:col-span-2">
            {editing && (
              <button
                type="button"
                onClick={() => setDraft(EMPTY_DRAFT)}
                className="rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
              >
                Cancel
              </button>
            )}
            <button
              type="submit"
              className="rounded-lg bg-brand-600 px-4 py-2 text-sm font-semibold text-white hover:bg-brand-700"
            >
              {editing ? "Save changes" : "Create product"}
            </button>
          </div>
        </form>
      </section>

      <section className="rounded-2xl border border-slate-200 bg-white shadow-sm">
        <div className="flex flex-col gap-3 border-b border-slate-200 p-4 md:flex-row md:items-center md:justify-between">
          <h2 className="text-lg font-semibold text-slate-900">Products</h2>
          <div className="flex flex-wrap items-center gap-2">
            <select
              value={filterCategory}
              onChange={(e) => { setFilterCategory(e.target.value); setPage(1); }}
              className="rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-sm"
            >
              <option value="">All categories</option>
              {categories.map((c) => <option key={c.id} value={c.slug}>{c.name}</option>)}
            </select>
            <input
              type="search"
              value={search}
              onChange={(e) => { setSearch(e.target.value); setPage(1); }}
              placeholder="Search SKU or name…"
              className="rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-sm"
            />
          </div>
        </div>
        <div className="overflow-x-auto">
          <table className="min-w-full text-sm">
            <thead className="bg-slate-50 text-xs font-semibold uppercase tracking-wide text-slate-500">
              <tr>
                <th className="px-4 py-3 text-left">SKU</th>
                <th className="px-4 py-3 text-left">Product</th>
                <th className="px-4 py-3 text-left">Category</th>
                <th className="px-4 py-3 text-right">Price</th>
                <th className="px-4 py-3 text-right">Stock</th>
                <th className="px-4 py-3 text-left">Status</th>
                <th className="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-200">
              {loading && !products && (
                <tr><td colSpan={7} className="px-4 py-8 text-center text-sm text-slate-500">Loading…</td></tr>
              )}
              {products?.items.map((p) => (
                <tr key={p.id} className="hover:bg-slate-50">
                  <td className="px-4 py-3 font-mono text-xs text-slate-600">{p.sku}</td>
                  <td className="px-4 py-3 font-medium text-slate-900">{p.name}</td>
                  <td className="px-4 py-3 text-slate-600">{p.categoryName}</td>
                  <td className="px-4 py-3 text-right font-medium text-slate-900">
                    {formatMoney(p.priceMinor, p.currency)}
                  </td>
                  <td className="px-4 py-3 text-right">
                    <span className={p.stockOnHand <= 0 ? "text-red-600 font-semibold" : p.isLowStock ? "text-amber-600 font-semibold" : "text-slate-900"}>
                      {p.stockOnHand}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <StatusPill active={p.isActive} />
                  </td>
                  <td className="px-4 py-3 text-right">
                    <div className="inline-flex items-center gap-1">
                      <IconBtn title="-1 stock" onClick={() => onAdjust(p.id, -1)}>−</IconBtn>
                      <IconBtn title="+1 stock" onClick={() => onAdjust(p.id, +1)}>+</IconBtn>
                      <button
                        type="button"
                        onClick={() => onEdit(p)}
                        className="rounded-md border border-slate-200 bg-white px-2 py-1 text-xs font-medium text-slate-700 hover:bg-slate-50"
                      >
                        Edit
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
              {products && products.items.length === 0 && (
                <tr><td colSpan={7} className="px-4 py-8 text-center text-sm text-slate-500">No products yet.</td></tr>
              )}
            </tbody>
          </table>
        </div>
        {products && products.totalPages > 1 && (
          <div className="flex items-center justify-between border-t border-slate-200 px-4 py-3 text-sm">
            <span className="text-slate-500">
              Page {products.page} of {products.totalPages} · {products.totalCount} items
            </span>
            <div className="flex items-center gap-2">
              <PageBtn disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Prev</PageBtn>
              <PageBtn disabled={page >= products.totalPages} onClick={() => setPage((p) => p + 1)}>Next</PageBtn>
            </div>
          </div>
        )}
      </section>
    </div>
  );
}

function CategoryForm({ onCreated }: { onCreated: () => Promise<void> }) {
  const [draft, setDraft] = useState<CreateCategoryInput>({
    name: "", slug: "", description: "", displayOrder: 0, isActive: true,
  });
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  return (
    <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
      <h2 className="text-lg font-semibold text-slate-900">New category</h2>
      {err && <Banner kind="error" message={err} />}
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setErr(null); setBusy(true);
          try {
            await createCategory({
              ...draft,
              slug: draft.slug?.trim() || undefined,
              description: draft.description?.trim() || undefined,
            });
            setDraft({ name: "", slug: "", description: "", displayOrder: 0, isActive: true });
            await onCreated();
          } catch (e) { setErr((e as Error).message); }
          finally { setBusy(false); }
        }}
        className="mt-4 grid gap-4 lg:grid-cols-2"
      >
        <Field label="Name">
          <input required className={inputCls} value={draft.name}
                 onChange={(e) => setDraft({ ...draft, name: e.target.value })} />
        </Field>
        <Field label="Slug (optional, auto-generated)">
          <input className={inputCls} value={draft.slug ?? ""}
                 onChange={(e) => setDraft({ ...draft, slug: e.target.value })} />
        </Field>
        <Field label="Description (optional)" colSpan={2}>
          <textarea rows={2} className={inputCls} value={draft.description ?? ""}
                    onChange={(e) => setDraft({ ...draft, description: e.target.value })} />
        </Field>
        <Field label="Display order">
          <input type="number" className={inputCls} value={draft.displayOrder ?? 0}
                 onChange={(e) => setDraft({ ...draft, displayOrder: parseInt(e.target.value, 10) || 0 })} />
        </Field>
        <div className="flex items-end justify-end lg:col-span-2">
          <button type="submit" disabled={busy}
                  className="rounded-lg bg-brand-600 px-4 py-2 text-sm font-semibold text-white hover:bg-brand-700 disabled:opacity-50">
            {busy ? "Creating…" : "Create category"}
          </button>
        </div>
      </form>
    </section>
  );
}

const inputCls =
  "w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500";

function Field({
  label, children, colSpan,
}: { label: string; children: React.ReactNode; colSpan?: number }) {
  return (
    <label className={`block ${colSpan === 2 ? "lg:col-span-2" : ""}`}>
      <span className="mb-1 block text-xs font-semibold uppercase tracking-wide text-slate-600">
        {label}
      </span>
      {children}
    </label>
  );
}

function Banner({ kind, message }: { kind: "error" | "success"; message: string }) {
  const cls = kind === "error"
    ? "border-red-200 bg-red-50 text-red-700"
    : "border-emerald-200 bg-emerald-50 text-emerald-700";
  return <div className={`rounded-lg border px-4 py-3 text-sm ${cls}`}>{message}</div>;
}

function StatusPill({ active }: { active: boolean }) {
  return active ? (
    <span className="inline-flex items-center rounded-full bg-emerald-50 px-2 py-0.5 text-xs font-semibold text-emerald-700">
      Active
    </span>
  ) : (
    <span className="inline-flex items-center rounded-full bg-slate-100 px-2 py-0.5 text-xs font-semibold text-slate-700">
      Inactive
    </span>
  );
}

function IconBtn({
  children, onClick, title,
}: {
  children: React.ReactNode;
  onClick: () => void;
  title: string;
}) {
  return (
    <button
      type="button"
      title={title}
      onClick={onClick}
      className="inline-flex h-7 w-7 items-center justify-center rounded-md border border-slate-200 bg-white text-sm font-bold text-slate-700 hover:bg-slate-50"
    >
      {children}
    </button>
  );
}

function PageBtn({
  children, disabled, onClick,
}: {
  children: React.ReactNode;
  disabled?: boolean;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={onClick}
      className="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-sm font-medium hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
    >
      {children}
    </button>
  );
}
