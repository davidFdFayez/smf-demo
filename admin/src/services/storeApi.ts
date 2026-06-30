import axios from "axios";
import { httpClient } from "./httpClient";

/**
 * Admin storefront client. Talks to the Phase 4 API surface defined in
 * <c>SMF.Api/Endpoints/StoreEndpoints.cs</c> + <c>InvoiceEndpoints.cs</c>.
 *
 * Pure read/write helpers — no caching here so the operator console can
 * always see the latest stock and order status.
 */

export class ApiError extends Error {
  constructor(public readonly status: number | undefined, message: string) {
    super(message);
    this.name = "ApiError";
  }
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface CategoryDto {
  id: string;
  name: string;
  slug: string;
  description?: string | null;
  imageUrl?: string | null;
  displayOrder: number;
  isActive: boolean;
}

export interface ProductSummaryDto {
  id: string;
  sku: string;
  name: string;
  imageUrl?: string | null;
  priceMinor: number;
  currency: string;
  stockOnHand: number;
  isActive: boolean;
  isLowStock: boolean;
  categoryId: string;
  categoryName: string;
}

export interface ProductDetailDto extends ProductSummaryDto {
  description?: string | null;
  lowStockThreshold?: number | null;
  categorySlug: string;
}

export type OrderStatus =
  | "AwaitingPayment"
  | "Paid"
  | "Fulfilled"
  | "Cancelled"
  | "Refunded";

export interface OrderListItem {
  id: string;
  orderNumber: string;
  memberId?: string | null;
  buyerName: string;
  buyerEmail: string;
  totalMinor: number;
  currency: string;
  status: OrderStatus;
  itemCount: number;
  createdAtUtc: string;
}

export interface OrderShippingAddressDto {
  recipientName: string;
  line1: string;
  line2?: string | null;
  city: string;
  region: string;
  postalCode: string;
  country: string;
  phoneNumber: string;
}

export interface OrderLineDto {
  productId: string;
  productSku: string;
  productName: string;
  unitPriceMinor: number;
  quantity: number;
  lineTotalMinor: number;
}

export interface OrderDto {
  id: string;
  orderNumber: string;
  memberId?: string | null;
  buyerName: string;
  buyerEmail: string;
  shippingAddress: OrderShippingAddressDto;
  subtotalMinor: number;
  vatRateBp: number;
  vatAmountMinor: number;
  shippingFeeMinor: number;
  totalMinor: number;
  currency: string;
  status: OrderStatus;
  paymentId?: string | null;
  invoiceId?: string | null;
  createdAtUtc: string;
  paidAtUtc?: string | null;
  items: OrderLineDto[];
}

export interface CreateProductInput {
  categoryId: string;
  sku: string;
  name: string;
  description?: string;
  imageUrl?: string;
  priceMinor: number;
  initialStock: number;
  lowStockThreshold?: number;
  currency?: string;
}

export interface UpdateProductInput {
  categoryId: string;
  name: string;
  description?: string | null;
  imageUrl?: string | null;
  priceMinor: number;
  lowStockThreshold?: number | null;
  isActive: boolean;
}

export interface CreateCategoryInput {
  name: string;
  slug?: string;
  description?: string;
  imageUrl?: string;
  displayOrder?: number;
  isActive?: boolean;
}

export async function listCategories(): Promise<CategoryDto[]> {
  return wrap(() =>
    httpClient.get<CategoryDto[]>("/api/store/categories", {
      params: { includeInactive: true },
    }).then((r) => r.data),
  );
}

export async function createCategory(input: CreateCategoryInput): Promise<string> {
  return wrap(async () => {
    const slug = (input.slug?.trim() || slugify(input.name));
    const payload = { ...input, slug };
    const { data } = await httpClient.post<{ id: string }>(
      "/api/admin/store/categories", payload);
    return data.id;
  });
}

function slugify(value: string): string {
  return value
    .toLowerCase()
    .normalize("NFKD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 120) || "category";
}

export interface ListProductsArgs {
  page?: number;
  pageSize?: number;
  search?: string;
  category?: string;
}

export async function listProducts(
  args: ListProductsArgs = {},
): Promise<PagedResult<ProductSummaryDto>> {
  return wrap(() =>
    httpClient.get<PagedResult<ProductSummaryDto>>("/api/store/products", {
      params: clean(args),
    }).then((r) => r.data),
  );
}

export async function getProduct(id: string): Promise<ProductDetailDto> {
  return wrap(() =>
    httpClient.get<ProductDetailDto>(
      `/api/store/products/${encodeURIComponent(id)}`,
    ).then((r) => r.data),
  );
}

export async function createProduct(input: CreateProductInput): Promise<string> {
  return wrap(async () => {
    const payload = { currency: "SAR", ...input };
    const { data } = await httpClient.post<{ id: string }>(
      "/api/admin/store/products", payload);
    return data.id;
  });
}

export async function updateProduct(
  id: string, input: UpdateProductInput,
): Promise<void> {
  return wrap(() =>
    httpClient.put(`/api/admin/store/products/${encodeURIComponent(id)}`, input)
      .then(() => undefined),
  );
}

export async function adjustStock(
  id: string, delta: number, reason?: string,
): Promise<void> {
  return wrap(() =>
    httpClient.post(
      `/api/admin/store/products/${encodeURIComponent(id)}/stock`,
      { delta, reason },
    ).then(() => undefined),
  );
}

export interface ListOrdersArgs {
  page?: number;
  pageSize?: number;
  status?: OrderStatus;
  memberId?: string;
  search?: string;
}

export async function listOrders(
  args: ListOrdersArgs = {},
): Promise<PagedResult<OrderListItem>> {
  return wrap(() =>
    httpClient.get<PagedResult<OrderListItem>>("/api/admin/store/orders", {
      params: clean(args),
    }).then((r) => r.data),
  );
}

export async function getOrder(id: string): Promise<OrderDto> {
  return wrap(() =>
    httpClient.get<OrderDto>(`/api/store/orders/${encodeURIComponent(id)}`)
      .then((r) => r.data),
  );
}

export async function fulfillOrder(id: string): Promise<void> {
  return wrap(() =>
    httpClient.post(`/api/admin/store/orders/${encodeURIComponent(id)}/fulfill`)
      .then(() => undefined),
  );
}

export async function cancelOrder(id: string, reason?: string): Promise<void> {
  return wrap(() =>
    httpClient.post(
      `/api/admin/store/orders/${encodeURIComponent(id)}/cancel`,
      { reason },
    ).then(() => undefined),
  );
}

export function invoiceDocumentUrl(invoiceId: string, format: "html" | "pdf" = "pdf"): string {
  const base = (import.meta.env.VITE_API_BASE_URL ?? "").toString().replace(/\/$/, "");
  return `${base}/api/invoices/${encodeURIComponent(invoiceId)}/document?format=${format}`;
}

export function formatMoney(minor: number, currency = "SAR"): string {
  const amount = minor / 100;
  try {
    return new Intl.NumberFormat("en-SA", {
      style: "currency", currency, maximumFractionDigits: 2,
    }).format(amount);
  } catch {
    return `${amount.toFixed(2)} ${currency}`;
  }
}

function clean<T extends object>(input: T): Partial<T> {
  const out: Record<string, unknown> = {};
  for (const [k, v] of Object.entries(input)) {
    if (v === undefined || v === null || v === "") continue;
    out[k] = v;
  }
  return out as Partial<T>;
}

async function wrap<T>(fn: () => Promise<T>): Promise<T> {
  try {
    return await fn();
  } catch (err) {
    if (axios.isAxiosError(err)) {
      const status = err.response?.status;
      const body = err.response?.data as
        | { title?: string; detail?: string; errors?: Record<string, string[]> }
        | undefined;
      const flat = body?.errors
        ? Object.values(body.errors).flat().join("\n")
        : undefined;
      throw new ApiError(
        status,
        flat ?? body?.detail ?? body?.title ?? err.message ?? "Request failed.",
      );
    }
    throw err;
  }
}
