import axios from "axios";
import { httpClient } from "./httpClient";
import { ApiError, ApiValidationError } from "./membersApi";

/**
 * Storefront / e-commerce client. Mirrors the backend
 * <c>StoreEndpoints</c> in <c>SMF.Api/Endpoints/StoreEndpoints.cs</c>.
 *
 * Cart identity is supplied through the <c>X-Cart-Key</c> header — a UUID
 * generated on first use and persisted in <c>localStorage</c>. Authenticated
 * members get their cart resolved server-side via the JWT, but the guest
 * key is kept around so the cart can survive sign-in/out cycles.
 */

const CART_KEY_STORAGE = "smf.store.cartKey";

export type Currency = "SAR" | string;

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
  currency: Currency;
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

export interface CartLineDto {
  productId: string;
  productSku: string;
  productName: string;
  imageUrl?: string | null;
  unitPriceMinor: number;
  quantity: number;
  lineTotalMinor: number;
}

export interface CartDto {
  id: string;
  guestKey?: string | null;
  memberId?: string | null;
  currency: Currency;
  subtotalMinor: number;
  vatRateBp: number;
  estimatedVatMinor: number;
  estimatedTotalMinor: number;
  items: CartLineDto[];
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
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

export type OrderStatus =
  | "AwaitingPayment"
  | "Paid"
  | "Fulfilled"
  | "Cancelled"
  | "Refunded";

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
  currency: Currency;
  status: OrderStatus;
  paymentId?: string | null;
  invoiceId?: string | null;
  createdAtUtc: string;
  paidAtUtc?: string | null;
  items: OrderLineDto[];
}

export interface CheckoutResultDto {
  orderId: string;
  orderNumber: string;
  paymentId: string;
  providerTransactionId: string;
  redirectUrl: string;
  totalMinor: number;
  currency: Currency;
}

export type PaymentProvider = "Mada" | "ApplePay" | "Visa" | "Mastercard" | "Tap";

export interface CheckoutRequest {
  buyerName: string;
  buyerEmail: string;
  buyerTaxNumber?: string;
  shippingAddress: OrderShippingAddressDto;
  shippingFeeMinor: number;
  provider: PaymentProvider;
  callbackUrl: string;
  memberId?: string | null;
}

export function getOrCreateCartKey(): string {
  if (typeof window === "undefined") return crypto.randomUUID();
  const existing = window.localStorage.getItem(CART_KEY_STORAGE);
  if (existing) return existing;
  const fresh = crypto.randomUUID();
  window.localStorage.setItem(CART_KEY_STORAGE, fresh);
  return fresh;
}

export function clearCartKey(): void {
  if (typeof window !== "undefined") {
    window.localStorage.removeItem(CART_KEY_STORAGE);
  }
}

function cartHeaders() {
  return { "X-Cart-Key": getOrCreateCartKey() };
}

export async function listCategories(): Promise<CategoryDto[]> {
  try {
    const { data } = await httpClient.get<CategoryDto[]>("/api/store/categories");
    return data;
  } catch (err) {
    throw toApiError(err);
  }
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
  try {
    const { data } = await httpClient.get<PagedResult<ProductSummaryDto>>(
      "/api/store/products",
      { params: clean(args) },
    );
    return data;
  } catch (err) {
    throw toApiError(err);
  }
}

export async function getProduct(id: string): Promise<ProductDetailDto> {
  try {
    const { data } = await httpClient.get<ProductDetailDto>(
      `/api/store/products/${encodeURIComponent(id)}`,
    );
    return data;
  } catch (err) {
    throw toApiError(err);
  }
}

export async function getCart(): Promise<CartDto> {
  try {
    const { data } = await httpClient.get<CartDto>("/api/store/cart", {
      headers: cartHeaders(),
    });
    return data;
  } catch (err) {
    throw toApiError(err);
  }
}

export async function addCartItem(
  productId: string, quantity = 1,
): Promise<CartDto> {
  try {
    const { data } = await httpClient.post<CartDto>(
      "/api/store/cart/items",
      { productId, quantity },
      { headers: cartHeaders() },
    );
    return data;
  } catch (err) {
    throw toApiError(err);
  }
}

export async function updateCartItem(
  productId: string, quantity: number,
): Promise<CartDto> {
  try {
    const { data } = await httpClient.patch<CartDto>(
      `/api/store/cart/items/${encodeURIComponent(productId)}`,
      { quantity },
      { headers: cartHeaders() },
    );
    return data;
  } catch (err) {
    throw toApiError(err);
  }
}

export async function removeCartItem(productId: string): Promise<CartDto> {
  try {
    const { data } = await httpClient.delete<CartDto>(
      `/api/store/cart/items/${encodeURIComponent(productId)}`,
      { headers: cartHeaders() },
    );
    return data;
  } catch (err) {
    throw toApiError(err);
  }
}

export async function clearCart(): Promise<CartDto> {
  try {
    const { data } = await httpClient.delete<CartDto>("/api/store/cart", {
      headers: cartHeaders(),
    });
    return data;
  } catch (err) {
    throw toApiError(err);
  }
}

export async function checkoutCart(
  req: CheckoutRequest,
): Promise<CheckoutResultDto> {
  try {
    const { data } = await httpClient.post<CheckoutResultDto>(
      "/api/store/checkout",
      req,
      { headers: cartHeaders() },
    );
    return data;
  } catch (err) {
    throw toApiError(err);
  }
}

export async function getOrder(id: string): Promise<OrderDto> {
  try {
    const { data } = await httpClient.get<OrderDto>(
      `/api/store/orders/${encodeURIComponent(id)}`,
    );
    return data;
  } catch (err) {
    throw toApiError(err);
  }
}

export function invoiceDocumentUrl(invoiceId: string, format: "html" | "pdf" = "pdf"): string {
  const base = (import.meta.env.VITE_API_BASE_URL ?? "").toString().replace(/\/$/, "");
  return `${base}/api/invoices/${encodeURIComponent(invoiceId)}/document?format=${format}`;
}

export function formatMoney(minor: number, currency: Currency = "SAR"): string {
  const amount = minor / 100;
  try {
    return new Intl.NumberFormat("en-SA", {
      style: "currency",
      currency,
      maximumFractionDigits: 2,
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

function toApiError(err: unknown): Error {
  if (axios.isAxiosError(err)) {
    const status = err.response?.status;
    const body = err.response?.data as
      | { title?: string; errors?: Record<string, string[]>; detail?: string }
      | undefined;

    if (status === 400 && body?.errors) {
      return new ApiValidationError(body.errors, body.title ?? "Validation failed.");
    }
    return new ApiError(
      status,
      body?.detail ?? body?.title ?? err.message ?? "Request failed.",
    );
  }
  return new ApiError(undefined, (err as Error).message ?? "Network error.");
}
