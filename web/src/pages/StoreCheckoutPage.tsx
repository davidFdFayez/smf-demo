import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import {
  checkoutCart,
  formatMoney,
  type CheckoutRequest,
  type PaymentProvider,
} from "../services/storeApi";
import { useCart } from "../features/store/useCart";

interface FormState {
  buyerName: string;
  buyerEmail: string;
  buyerTaxNumber: string;
  recipientName: string;
  line1: string;
  line2: string;
  city: string;
  region: string;
  postalCode: string;
  country: string;
  phoneNumber: string;
  provider: PaymentProvider;
}

const SHIPPING_FEE_MINOR = 2500; // SAR 25 flat — match server-side fulfilment policy.

const INITIAL: FormState = {
  buyerName: "",
  buyerEmail: "",
  buyerTaxNumber: "",
  recipientName: "",
  line1: "",
  line2: "",
  city: "",
  region: "",
  postalCode: "",
  country: "SA",
  phoneNumber: "",
  provider: "Mada",
};

export function StoreCheckoutPage() {
  const navigate = useNavigate();
  const { cart, refresh } = useCart();
  const [form, setForm] = useState<FormState>(INITIAL);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});

  useEffect(() => {
    if (cart && cart.items.length === 0) {
      navigate("/store/cart", { replace: true });
    }
  }, [cart, navigate]);

  const grandTotal = useMemo(
    () => (cart?.estimatedTotalMinor ?? 0) + SHIPPING_FEE_MINOR,
    [cart],
  );

  const update = (k: keyof FormState) =>
    (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) =>
      setForm((prev) => ({ ...prev, [k]: e.target.value }));

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!cart || cart.items.length === 0) return;
    setError(null);
    setFieldErrors({});
    setSubmitting(true);

    const request: CheckoutRequest = {
      buyerName: form.buyerName.trim(),
      buyerEmail: form.buyerEmail.trim(),
      buyerTaxNumber: form.buyerTaxNumber.trim() || undefined,
      shippingAddress: {
        recipientName: form.recipientName.trim() || form.buyerName.trim(),
        line1: form.line1.trim(),
        line2: form.line2.trim() || undefined,
        city: form.city.trim(),
        region: form.region.trim(),
        postalCode: form.postalCode.trim(),
        country: form.country.trim().toUpperCase(),
        phoneNumber: form.phoneNumber.trim(),
      },
      shippingFeeMinor: SHIPPING_FEE_MINOR,
      provider: form.provider,
      callbackUrl: `${window.location.origin}/store/orders/{orderId}/result`,
    };

    try {
      const result = await checkoutCart({
        ...request,
        callbackUrl: `${window.location.origin}/store/orders/result?paymentId={paymentId}`,
      });

      // Use the gateway-provided redirect URL when present (real Tap flow).
      // The local sandbox returns a recognisable URL; in that case skip the
      // hop and go straight to the order confirmation page.
      const sandbox = /sandbox\.smf\.local|localhost/.test(result.redirectUrl);
      if (!sandbox) {
        window.location.href = result.redirectUrl;
        return;
      }

      await refresh();
      navigate(`/store/orders/${result.orderId}?payment=${result.paymentId}`);
    } catch (err) {
      const e = err as { errors?: Record<string, string[]>; message?: string };
      if (e.errors) {
        setFieldErrors(e.errors);
        setError("Please correct the highlighted fields.");
      } else {
        setError(e.message ?? "Checkout failed.");
      }
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6">
      <h1 className="text-3xl font-bold text-slate-900">Checkout</h1>

      {error && (
        <div className="mt-4 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {error}
        </div>
      )}

      <form onSubmit={onSubmit} className="mt-8 grid gap-8 lg:grid-cols-[1fr_360px]">
        <div className="space-y-6">
          <Section title="Contact details">
            <Field label="Full name" error={firstErr(fieldErrors, "BuyerName")}>
              <input className={inputClass} required value={form.buyerName} onChange={update("buyerName")} />
            </Field>
            <Field label="Email" error={firstErr(fieldErrors, "BuyerEmail")}>
              <input className={inputClass} required type="email" value={form.buyerEmail} onChange={update("buyerEmail")} />
            </Field>
            <Field label="VAT / Tax number (optional)" error={firstErr(fieldErrors, "BuyerTaxNumber")}>
              <input className={inputClass} value={form.buyerTaxNumber} onChange={update("buyerTaxNumber")} />
            </Field>
          </Section>

          <Section title="Shipping address">
            <Field label="Recipient name" error={firstErr(fieldErrors, "ShippingAddress.RecipientName")}>
              <input className={inputClass} value={form.recipientName} onChange={update("recipientName")} placeholder="Same as full name if blank" />
            </Field>
            <Field label="Address line 1" error={firstErr(fieldErrors, "ShippingAddress.Line1")}>
              <input className={inputClass} required value={form.line1} onChange={update("line1")} />
            </Field>
            <Field label="Address line 2 (optional)">
              <input className={inputClass} value={form.line2} onChange={update("line2")} />
            </Field>
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="City" error={firstErr(fieldErrors, "ShippingAddress.City")}>
                <input className={inputClass} required value={form.city} onChange={update("city")} />
              </Field>
              <Field label="Region" error={firstErr(fieldErrors, "ShippingAddress.Region")}>
                <input className={inputClass} required value={form.region} onChange={update("region")} />
              </Field>
              <Field label="Postal code" error={firstErr(fieldErrors, "ShippingAddress.PostalCode")}>
                <input className={inputClass} required value={form.postalCode} onChange={update("postalCode")} />
              </Field>
              <Field label="Country (ISO-2)" error={firstErr(fieldErrors, "ShippingAddress.Country")}>
                <input className={inputClass} required maxLength={2} value={form.country} onChange={update("country")} />
              </Field>
            </div>
            <Field label="Phone number" error={firstErr(fieldErrors, "ShippingAddress.PhoneNumber")}>
              <input className={inputClass} required value={form.phoneNumber} onChange={update("phoneNumber")} placeholder="+966…" />
            </Field>
          </Section>

          <Section title="Payment method">
            <div className="grid gap-3 sm:grid-cols-2">
              <ProviderRadio
                value="Mada"
                current={form.provider}
                onChange={(p) => setForm((f) => ({ ...f, provider: p }))}
                title="Mada"
                hint="Saudi domestic debit network"
              />
              <ProviderRadio
                value="ApplePay"
                current={form.provider}
                onChange={(p) => setForm((f) => ({ ...f, provider: p }))}
                title="Apple Pay"
                hint="Touch / Face ID, Mada-backed"
              />
              <ProviderRadio
                value="Visa"
                current={form.provider}
                onChange={(p) => setForm((f) => ({ ...f, provider: p }))}
                title="Visa"
                hint="International credit / debit"
              />
              <ProviderRadio
                value="Mastercard"
                current={form.provider}
                onChange={(p) => setForm((f) => ({ ...f, provider: p }))}
                title="Mastercard"
                hint="International credit / debit"
              />
            </div>
            <p className="mt-2 text-xs text-slate-500">
              You'll be redirected to the secure Tap Payments hosted page to
              complete this transaction.
            </p>
          </Section>
        </div>

        <aside className="h-fit rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <h2 className="text-base font-semibold text-slate-900">Order summary</h2>
          <ul className="mt-4 space-y-2 text-sm">
            {cart?.items.map((line) => (
              <li key={line.productId} className="flex justify-between gap-2">
                <span className="line-clamp-1 text-slate-700">
                  {line.productName} × {line.quantity}
                </span>
                <span className="font-medium text-slate-900">
                  {formatMoney(line.lineTotalMinor, cart?.currency)}
                </span>
              </li>
            ))}
          </ul>

          <dl className="mt-4 space-y-2 border-t border-slate-200 pt-4 text-sm">
            <Row label="Subtotal" value={formatMoney(cart?.subtotalMinor ?? 0, cart?.currency)} />
            <Row label={`VAT (${((cart?.vatRateBp ?? 0) / 100).toFixed(0)}%)`} value={formatMoney(cart?.estimatedVatMinor ?? 0, cart?.currency)} />
            <Row label="Shipping" value={formatMoney(SHIPPING_FEE_MINOR, cart?.currency)} />
            <div className="mt-3 flex justify-between border-t border-slate-200 pt-3 text-base font-bold text-slate-900">
              <dt>Total due</dt>
              <dd className="text-smf-700">{formatMoney(grandTotal, cart?.currency)}</dd>
            </div>
          </dl>

          <button
            type="submit"
            disabled={submitting}
            className="mt-6 inline-flex w-full items-center justify-center rounded-lg bg-smf-700 px-4 py-3 text-sm font-semibold text-white transition hover:bg-smf-600 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {submitting ? "Processing…" : `Pay ${formatMoney(grandTotal, cart?.currency)}`}
          </button>
          <Link to="/store/cart" className="mt-3 block text-center text-xs text-slate-500 hover:underline">
            ← Back to cart
          </Link>
        </aside>
      </form>
    </div>
  );
}

const inputClass =
  "w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 placeholder:text-slate-400 focus:border-smf-500 focus:outline-none focus:ring-1 focus:ring-smf-500";

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
      <h2 className="text-base font-semibold text-slate-900">{title}</h2>
      <div className="mt-4 space-y-4">{children}</div>
    </section>
  );
}

function Field({
  label, children, error,
}: { label: string; children: React.ReactNode; error?: string | null }) {
  return (
    <label className="block">
      <span className="mb-1 block text-xs font-semibold uppercase tracking-wide text-slate-600">
        {label}
      </span>
      {children}
      {error && <p className="mt-1 text-xs text-red-600">{error}</p>}
    </label>
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

function ProviderRadio({
  value, current, onChange, title, hint,
}: {
  value: PaymentProvider;
  current: PaymentProvider;
  onChange: (p: PaymentProvider) => void;
  title: string;
  hint: string;
}) {
  const active = current === value;
  return (
    <button
      type="button"
      onClick={() => onChange(value)}
      className={
        "flex flex-col items-start rounded-xl border px-4 py-3 text-left transition " +
        (active
          ? "border-smf-500 bg-smf-50 ring-1 ring-smf-500"
          : "border-slate-200 bg-white hover:border-smf-300")
      }
    >
      <span className="text-sm font-semibold text-slate-900">{title}</span>
      <span className="mt-0.5 text-xs text-slate-500">{hint}</span>
    </button>
  );
}

function firstErr(errs: Record<string, string[]>, key: string): string | null {
  return errs[key]?.[0] ?? null;
}
