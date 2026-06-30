import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import clsx from "clsx";
import { ApiError } from "../../services/membersApi";
import { listMembers } from "../../services/membersApi";
import {
  initializePayment,
  PaymentAbortedError,
} from "../../services/paymentsApi";
import { RegistrationStatus, type MemberListItem } from "../members/schema";
import {
  PaymentProvider,
  PaymentPurpose,
  breakdownVat,
  formatMinor,
  invoiceTotalMinor,
  type Invoice,
} from "./types";
import {
  directionFor,
  intlLocaleFor,
  stringsFor,
  type CheckoutT,
  type Locale,
} from "./i18n";
import { isApplePayAvailable, memberIdFromLocation } from "./platform";

/**
 * Demo invoice. In a real app this would come from a pricing service
 * (or a cart). Amounts are in **minor units** (halalas) and are always
 * stored VAT-inclusive — the UI splits the display via `breakdownVat`.
 *
 *    200.00 SAR (gross)  →  20 000 halalas
 */
const DEFAULT_INVOICE: Invoice = {
  purpose: PaymentPurpose.MembershipFee,
  currency: "SAR",
  lines: [
    {
      // `label` and `description` are i18n keys into CheckoutT; the render
      // pass resolves them per locale. This keeps the invoice translatable
      // without turning it into a giant template.
      label: "annualLicenseFee",
      description: "annualLicenseFeeDescription",
      amountMinor: 20_000,
    },
  ],
};

type MethodDef = {
  provider: PaymentProvider;
  glyph: ReactNode;
  /** Derived per-render from browser capability detection. */
  available: boolean;
  unavailableReason?: string;
};

const BASE_METHODS = [
  {
    provider: PaymentProvider.Mada,
    glyph: <BrandGlyph text="mada" className="bg-emerald-600" />,
    alwaysAvailable: true,
  },
  {
    provider: PaymentProvider.ApplePay,
    glyph: <BrandGlyph text="Pay" className="bg-slate-900" />,
    alwaysAvailable: false,
  },
  {
    provider: PaymentProvider.Visa,
    glyph: <BrandGlyph text="VISA" className="bg-blue-700" />,
    alwaysAvailable: true,
  },
] as const;

const LOCALE_STORAGE_KEY = "smf.checkout.locale";

/**
 * Checkout & Payment UI.
 *
 * Flow:
 *   1. Pick a member (auto-populated from ?memberId= if present).
 *   2. Render the invoice + pick a payment method (Apple Pay only shown
 *      when `window.ApplePaySession.canMakePayments()`).
 *   3. Click Pay Now → POST /api/payments → full-page redirect to the
 *      provider's `redirectUrl`. The webhook flips the Payment to
 *      Succeeded and the outbox promotes the Member to Active.
 *
 * The component is bilingual (English / Arabic) with live RTL flip and
 * ar-SA currency formatting.
 */
export function CheckoutPage() {
  // ─── locale ────────────────────────────────────────────────────────
  const [locale, setLocaleState] = useState<Locale>(() => initialLocale());
  const t = useMemo<CheckoutT>(() => stringsFor(locale), [locale]);
  const dir = directionFor(locale);
  const intlLocale = intlLocaleFor(locale);

  const setLocale = useCallback((next: Locale) => {
    setLocaleState(next);
    try {
      window.localStorage.setItem(LOCALE_STORAGE_KEY, next);
    } catch {
      // Private mode or storage quota — ignore.
    }
  }, []);

  // ─── data ──────────────────────────────────────────────────────────
  const [members, setMembers] = useState<MemberListItem[]>([]);
  const [membersLoading, setMembersLoading] = useState(false);
  const [membersError, setMembersError] = useState<string | null>(null);
  const [selectedMemberId, setSelectedMemberId] = useState<string>("");

  const [invoice] = useState<Invoice>(DEFAULT_INVOICE);
  const [provider, setProvider] = useState<PaymentProvider>(PaymentProvider.Mada);

  const [submitting, setSubmitting] = useState(false);
  const [paymentError, setPaymentError] = useState<string | null>(null);
  const [redirectNotice, setRedirectNotice] = useState<string | null>(null);

  // ─── method availability (Apple Pay capability check) ─────────────
  const methods = useMemo<MethodDef[]>(() => {
    const applePayOk = isApplePayAvailable();
    return BASE_METHODS.map((m) => {
      if (m.provider === PaymentProvider.ApplePay) {
        return {
          provider: m.provider,
          glyph: m.glyph,
          available: applePayOk,
          unavailableReason: applePayOk ? undefined : t.applePayUnavailable,
        };
      }
      return {
        provider: m.provider,
        glyph: m.glyph,
        available: true,
      };
    });
  }, [t]);

  // Auto-demote if the user selected Apple Pay but it isn't available
  // (e.g. they switched browsers mid-session). Keep the default on mada.
  useEffect(() => {
    const selected = methods.find((m) => m.provider === provider);
    if (!selected || !selected.available) {
      setProvider(PaymentProvider.Mada);
    }
  }, [methods, provider]);

  // ─── load members (filtered to Approved) ──────────────────────────
  const loadMembers = useCallback(async (signal?: AbortSignal) => {
    setMembersLoading(true);
    setMembersError(null);
    try {
      const result = await listMembers(1, 200, {}, signal);
      const approved = result.items.filter(
        (m) => m.registrationStatus === RegistrationStatus.Approved,
      );
      setMembers(approved);

      // Preselect order of priority:
      //   1. current selection (if still valid)
      //   2. ?memberId= deep link (if it points at an Approved member)
      //   3. first Approved member
      setSelectedMemberId((current) => {
        if (current && approved.some((m) => m.id === current)) return current;
        const fromUrl = memberIdFromLocation();
        if (fromUrl && approved.some((m) => m.id === fromUrl)) return fromUrl;
        return approved[0]?.id ?? "";
      });
    } catch (err) {
      if (err instanceof ApiError) {
        setMembersError(
          err.status
            ? `${t.memberPickerError} (${err.status})`
            : err.message || t.memberPickerError,
        );
      } else {
        setMembersError(t.memberPickerError);
      }
    } finally {
      setMembersLoading(false);
    }
  }, [t]);

  useEffect(() => {
    const controller = new AbortController();
    void loadMembers(controller.signal);
    return () => controller.abort();
  }, [loadMembers]);

  // ─── pay-now lifecycle: cancels in-flight request on unmount ──────
  const payAbortRef = useRef<AbortController | null>(null);
  useEffect(
    () => () => {
      payAbortRef.current?.abort();
    },
    [],
  );

  const totalMinor = useMemo(() => invoiceTotalMinor(invoice), [invoice]);
  const vat = useMemo(() => breakdownVat(totalMinor), [totalMinor]);
  const selectedMember = useMemo(
    () => members.find((m) => m.id === selectedMemberId) ?? null,
    [members, selectedMemberId],
  );

  const formattedTotal = formatMinor(totalMinor, invoice.currency, intlLocale);
  const formattedSubtotal = formatMinor(vat.subtotalMinor, invoice.currency, intlLocale);
  const formattedVat = formatMinor(vat.vatMinor, invoice.currency, intlLocale);

  const handlePayNow = useCallback(async () => {
    if (!selectedMemberId || submitting) return;

    // Replace any previous in-flight attempt.
    payAbortRef.current?.abort();
    const controller = new AbortController();
    payAbortRef.current = controller;

    setSubmitting(true);
    setPaymentError(null);
    setRedirectNotice(null);

    try {
      const callbackUrl = `${window.location.origin}/api/payments/callback`;
      const result = await initializePayment(
        {
          memberId: selectedMemberId,
          provider,
          purpose: invoice.purpose,
          amountMinor: totalMinor,
          currency: invoice.currency,
          callbackUrl,
          description: invoice.lines.map((l) => t[l.label as keyof CheckoutT] as string).join(", "),
        },
        controller.signal,
      );

      setRedirectNotice(result.redirectUrl);
      window.location.href = result.redirectUrl;
    } catch (err) {
      if (err instanceof PaymentAbortedError) {
        // Silent: user navigated away, nothing to tell them.
        return;
      }
      if (err instanceof ApiError) {
        if (err.status === 404) {
          setPaymentError(t.memberNotFound);
        } else {
          setPaymentError(
            err.status ? `${err.message} (${err.status})` : err.message,
          );
        }
      } else {
        setPaymentError(err instanceof Error ? err.message : t.networkError);
      }
      setSubmitting(false);
    }
  }, [
    selectedMemberId,
    submitting,
    provider,
    invoice,
    totalMinor,
    t,
  ]);

  const handleCancel = useCallback(() => {
    payAbortRef.current?.abort();
    setSubmitting(false);
    setPaymentError(null);
    setRedirectNotice(null);
  }, []);

  const payDisabled =
    submitting || !selectedMemberId || totalMinor <= 0 || membersLoading;
  const providerLabel = localisedProviderLabel(provider, t);

  return (
    <section dir={dir} className="grid gap-6 lg:grid-cols-[1fr,360px]">
      {/* ─────────────────────────── left column ─────────────────────────── */}
      <div className="space-y-6">
        {/* Language toggle + small page intro */}
        <div className="flex items-center justify-between">
          <p className="text-sm text-slate-500">{t.subtitle}</p>
          <button
            type="button"
            onClick={() => setLocale(locale === "en" ? "ar" : "en")}
            aria-label={t.switchLanguage}
            className="rounded-full border border-slate-300 bg-white px-3 py-1 text-xs font-medium text-slate-700 hover:border-smf-500 hover:text-smf-700"
          >
            {t.switchLanguage}
          </button>
        </div>

        <MemberPicker
          t={t}
          dir={dir}
          members={members}
          selectedId={selectedMemberId}
          onChange={setSelectedMemberId}
          loading={membersLoading}
          error={membersError}
          onRetry={() => void loadMembers()}
        />

        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
          <div className="mb-4 flex items-baseline justify-between">
            <h3 className="text-base font-semibold text-slate-900">
              {t.methodSectionTitle}
            </h3>
            <p className="text-xs text-slate-500">{t.methodSecurityNote}</p>
          </div>

          <fieldset className="space-y-3" aria-label={t.methodSectionTitle}>
            <legend className="sr-only">{t.methodSectionTitle}</legend>
            {methods.map((method) => (
              <MethodTile
                key={method.provider}
                method={method}
                selected={provider === method.provider}
                submitting={submitting}
                onSelect={() => setProvider(method.provider)}
                t={t}
              />
            ))}
          </fieldset>

          <p className="mt-4 text-xs text-slate-500">
            {t.methodHandoffNote(providerLabel)}
          </p>
        </div>
      </div>

      {/* ─────────────────────────── invoice / pay panel ─────────────────── */}
      <aside className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm lg:sticky lg:top-6 lg:self-start">
        <div>
          <h3 className="text-base font-semibold text-slate-900">{t.invoiceTitle}</h3>
          <p className="text-xs text-slate-500">
            {selectedMember
              ? t.invoiceForMember(selectedMember.fullName, selectedMember.smF_ID)
              : t.invoiceNoMember}
          </p>
        </div>

        <ul className="divide-y divide-slate-100 rounded-lg border border-slate-200">
          {invoice.lines.map((line) => (
            <li
              key={line.label}
              className="flex items-start justify-between gap-3 px-3 py-3"
            >
              <div>
                <div className="text-sm font-medium text-slate-900">
                  {t[line.label as keyof CheckoutT] as string}
                </div>
                {line.description && (
                  <div className="text-xs text-slate-500">
                    {t[line.description as keyof CheckoutT] as string}
                  </div>
                )}
              </div>
              <div className="text-sm font-semibold tabular-nums text-slate-900">
                {formatMinor(line.amountMinor, invoice.currency, intlLocale)}
              </div>
            </li>
          ))}
        </ul>

        <dl className="space-y-1 text-sm">
          <div className="flex justify-between text-slate-500">
            <dt>{t.subtotalExclVat}</dt>
            <dd className="tabular-nums">{formattedSubtotal}</dd>
          </div>
          <div className="flex justify-between text-slate-500">
            <dt>{t.vatLabel(vat.ratePercent)}</dt>
            <dd className="tabular-nums">{formattedVat}</dd>
          </div>
          <div className="flex justify-between border-t border-slate-200 pt-2 text-base font-semibold text-slate-900">
            <dt>{t.total}</dt>
            <dd className="tabular-nums">{formattedTotal}</dd>
          </div>
        </dl>

        {paymentError && (
          <div
            role="alert"
            className="rounded-lg border border-red-300 bg-red-50 px-3 py-2 text-sm text-red-800"
          >
            {paymentError}
          </div>
        )}

        {redirectNotice && (
          <div
            role="status"
            className="rounded-lg border border-smf-300 bg-smf-50 px-3 py-2 text-xs text-smf-800"
          >
            {t.redirecting(providerLabel)}{" "}
            <a
              href={redirectNotice}
              className="font-semibold underline underline-offset-2"
            >
              {t.redirectFallback}
            </a>
            .
          </div>
        )}

        <button
          type="button"
          onClick={() => void handlePayNow()}
          disabled={payDisabled}
          className={clsx(
            "flex w-full items-center justify-center gap-2 rounded-xl bg-smf-600 px-4 py-3",
            "text-sm font-semibold text-white shadow-sm transition",
            "hover:bg-smf-700 focus:outline-none focus:ring-2 focus:ring-smf-500/50",
            "disabled:cursor-not-allowed disabled:bg-smf-600/60",
          )}
        >
          {submitting ? (
            <>
              <Spinner />
              <span>{t.redirecting(providerLabel)}</span>
            </>
          ) : (
            <>
              <span>{t.payNow(formattedTotal, providerLabel)}</span>
              <ChevronEnd dir={dir} />
            </>
          )}
        </button>

        {submitting && (
          <button
            type="button"
            onClick={handleCancel}
            className="w-full text-xs font-medium text-slate-500 underline-offset-2 hover:text-slate-700 hover:underline"
          >
            {t.cancel}
          </button>
        )}

        <p className="text-[11px] leading-relaxed text-slate-400">
          {t.legalDisclaimer}
        </p>
      </aside>
    </section>
  );
}

// ─────────────────────────────────────────── sub-components ───────────────────────────

interface MethodTileProps {
  method: MethodDef;
  selected: boolean;
  submitting: boolean;
  onSelect: () => void;
  t: CheckoutT;
}

function MethodTile({ method, selected, submitting, onSelect, t }: MethodTileProps) {
  const title = methodTitle(method.provider, t);
  const subtitle = methodSubtitle(method.provider, t);

  return (
    <label
      className={clsx(
        "flex cursor-pointer items-center gap-4 rounded-xl border px-4 py-3 transition",
        selected
          ? "border-smf-600 bg-smf-50/50 ring-2 ring-smf-500/30"
          : "border-slate-200 bg-white hover:border-slate-300",
        !method.available && "cursor-not-allowed opacity-60 hover:border-slate-200",
      )}
    >
      <input
        type="radio"
        name="payment-method"
        value={method.provider}
        checked={selected}
        disabled={!method.available || submitting}
        onChange={onSelect}
        className="h-4 w-4 accent-smf-600"
      />
      {method.glyph}
      <div className="flex-1">
        <div className="text-sm font-semibold text-slate-900">{title}</div>
        <div className="text-xs text-slate-500">
          {method.available ? subtitle : method.unavailableReason ?? subtitle}
        </div>
      </div>
      {!method.available && (
        <span className="rounded-full bg-slate-100 px-2 py-0.5 text-[10px] font-medium uppercase tracking-wide text-slate-500">
          {t.comingSoon}
        </span>
      )}
    </label>
  );
}

interface MemberPickerProps {
  t: CheckoutT;
  dir: "ltr" | "rtl";
  members: MemberListItem[];
  selectedId: string;
  onChange: (id: string) => void;
  loading: boolean;
  error: string | null;
  onRetry: () => void;
}

function MemberPicker({
  t,
  members,
  selectedId,
  onChange,
  loading,
  error,
  onRetry,
}: MemberPickerProps) {
  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
      <div className="mb-2 flex items-baseline justify-between">
        <h3 className="text-base font-semibold text-slate-900">{t.memberSectionTitle}</h3>
        <span className="text-xs text-slate-500">{t.memberSectionHint}</span>
      </div>

      {error ? (
        <div
          role="alert"
          className="mt-2 flex items-center justify-between gap-3 rounded-lg border border-red-300 bg-red-50 px-3 py-2 text-sm text-red-800"
        >
          <span>{error}</span>
          <button
            type="button"
            onClick={onRetry}
            className="rounded-md border border-red-300 bg-white px-2 py-1 text-xs font-medium text-red-700 hover:bg-red-100"
          >
            {t.retry}
          </button>
        </div>
      ) : loading ? (
        <p className="mt-2 text-sm text-slate-400">{t.memberPickerLoading}</p>
      ) : members.length === 0 ? (
        <p className="mt-2 text-sm text-slate-400">{t.memberPickerEmpty}</p>
      ) : (
        <select
          value={selectedId}
          onChange={(e) => onChange(e.target.value)}
          className="mt-2 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm shadow-sm focus:border-smf-500 focus:outline-none focus:ring-2 focus:ring-smf-500/30"
        >
          {members.map((m) => (
            <option key={m.id} value={m.id}>
              {m.fullName} — {m.smF_ID} ({m.role})
            </option>
          ))}
        </select>
      )}
    </div>
  );
}

function BrandGlyph({ text, className }: { text: string; className?: string }) {
  return (
    <span
      aria-hidden="true"
      className={clsx(
        "inline-flex h-8 w-14 items-center justify-center rounded-md text-[11px] font-bold uppercase tracking-tight text-white shadow-sm",
        className,
      )}
    >
      {text}
    </span>
  );
}

function Spinner() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" className="h-4 w-4 animate-spin text-white">
      <circle cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" fill="none" opacity="0.25" />
      <path d="M4 12a8 8 0 018-8" stroke="currentColor" strokeWidth="4" fill="none" strokeLinecap="round" />
    </svg>
  );
}

function ChevronEnd({ dir }: { dir: "ltr" | "rtl" }) {
  // Flip the glyph in RTL so "forward" always points at the inline-end.
  return (
    <svg
      viewBox="0 0 20 20"
      fill="currentColor"
      aria-hidden="true"
      className={clsx("h-4 w-4", dir === "rtl" && "-scale-x-100")}
    >
      <path
        fillRule="evenodd"
        d="M7.22 4.22a.75.75 0 011.06 0l5.25 5.25a.75.75 0 010 1.06l-5.25 5.25a.75.75 0 11-1.06-1.06L11.94 10 7.22 5.28a.75.75 0 010-1.06z"
        clipRule="evenodd"
      />
    </svg>
  );
}

// ─────────────────────────────────────────── helpers ──────────────────────────────────

function initialLocale(): Locale {
  if (typeof window === "undefined") return "en";
  try {
    const stored = window.localStorage.getItem(LOCALE_STORAGE_KEY);
    if (stored === "ar" || stored === "en") return stored;
  } catch {
    // ignore
  }
  const navLang = (window.navigator.language ?? "en").toLowerCase();
  return navLang.startsWith("ar") ? "ar" : "en";
}

function localisedProviderLabel(p: PaymentProvider, t: CheckoutT): string {
  switch (p) {
    case PaymentProvider.Mada:
      return t.madaTitle;
    case PaymentProvider.ApplePay:
      return t.applePayTitle;
    case PaymentProvider.Visa:
      return t.visaTitle;
    default:
      return p;
  }
}

function methodTitle(p: PaymentProvider, t: CheckoutT): string {
  return localisedProviderLabel(p, t);
}

function methodSubtitle(p: PaymentProvider, t: CheckoutT): string {
  switch (p) {
    case PaymentProvider.Mada:
      return t.madaSubtitle;
    case PaymentProvider.ApplePay:
      return t.applePaySubtitle;
    case PaymentProvider.Visa:
      return t.visaSubtitle;
    default:
      return "";
  }
}
