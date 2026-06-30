/**
 * Mirrors the backend `PaymentProvider` enum
 * (`src/SMF.Domain/Enums/PaymentProvider.cs`). Both sides use the
 * JsonStringEnumConverter so the wire format is the identifier itself.
 */
export const PaymentProvider = {
  Mada: "Mada",
  ApplePay: "ApplePay",
  Visa: "Visa",
} as const;
export type PaymentProvider =
  (typeof PaymentProvider)[keyof typeof PaymentProvider];

/** Mirrors `PaymentPurpose` on the backend. */
export const PaymentPurpose = {
  MembershipFee: "MembershipFee",
  EventFee: "EventFee",
} as const;
export type PaymentPurpose =
  (typeof PaymentPurpose)[keyof typeof PaymentPurpose];

/**
 * Line-level description of what the user is buying. Kept deliberately
 * small; future iterations can extend this into a proper cart.
 */
export interface InvoiceLine {
  label: string;
  description?: string;
  amountMinor: number;
}

export interface Invoice {
  purpose: PaymentPurpose;
  currency: string;
  lines: InvoiceLine[];
}

/** POST /api/payments request body. Matches `InitializePaymentRequest`. */
export interface InitializePaymentRequest {
  memberId: string;
  provider: PaymentProvider;
  purpose: PaymentPurpose;
  amountMinor: number;
  currency: string;
  callbackUrl: string;
  description?: string;
  /**
   * Correlates an event-fee payment with a specific EventRegistration row,
   * so the backend can confirm exactly the right registration once the
   * gateway webhook arrives. Leave undefined for membership fees.
   */
  eventRegistrationId?: string;
}

/** 201 Created response body. Matches `InitializePaymentResult`. */
export interface InitializePaymentResponse {
  paymentId: string;
  providerTransactionId: string;
  redirectUrl: string;
}

/** Sum the invoice line amounts in minor units. */
export function invoiceTotalMinor(invoice: Invoice): number {
  return invoice.lines.reduce((sum, line) => sum + line.amountMinor, 0);
}

/**
 * Saudi Arabia's standard VAT rate. Stored on the Invoice eventually —
 * today it's a constant because we only charge one kind of fee.
 */
export const KSA_VAT_RATE = 0.15;

export interface VatBreakdown {
  subtotalMinor: number;
  vatMinor: number;
  totalMinor: number;
  ratePercent: string;
}

/**
 * Splits a gross amount (VAT-inclusive, as stored on the Invoice) into
 * subtotal + VAT, rounded to whole minor units. Derivation is:
 *
 *    subtotal = round(total / (1 + rate))
 *    vat      = total - subtotal    // avoids rounding drift
 */
export function breakdownVat(
  totalMinor: number,
  rate = KSA_VAT_RATE,
): VatBreakdown {
  const subtotalMinor = Math.round(totalMinor / (1 + rate));
  const vatMinor = totalMinor - subtotalMinor;
  return {
    subtotalMinor,
    vatMinor,
    totalMinor,
    ratePercent: (rate * 100).toFixed(0),
  };
}

/**
 * Format a minor-unit amount into a localised currency string.
 * The locale defaults to en-US for stable rendering; pass "ar-SA" to
 * render Arabic-Indic digits and the ر.س symbol.
 */
export function formatMinor(
  amountMinor: number,
  currency: string,
  locale = "en-US",
): string {
  return new Intl.NumberFormat(locale, {
    style: "currency",
    currency,
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(amountMinor / 100);
}
