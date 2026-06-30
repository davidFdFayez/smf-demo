/**
 * Minimal, dependency-free bilingual strings for the checkout page.
 *
 * Scope is deliberately narrow — this file covers one screen. If / when
 * the rest of the app needs Arabic, swap for a real i18n library
 * (react-intl, i18next). The public API here mirrors what we'd want
 * from that eventual migration:
 *
 *   const { t, dir, locale, setLocale } = useCheckoutLocale();
 *   t("invoice.annualLicenseFee")
 *
 * Strings MUST round-trip through this object — no hard-coded labels in
 * the TSX, otherwise RTL and translation drift silently.
 */

export type Locale = "en" | "ar";

export type Direction = "ltr" | "rtl";

export const directionFor = (locale: Locale): Direction =>
  locale === "ar" ? "rtl" : "ltr";

/**
 * BCP-47 tag used when formatting currency / numbers / dates.
 * `ar-SA` gives us Arabic-Indic digits and SAR as "ر.س".
 */
export const intlLocaleFor = (locale: Locale): string =>
  locale === "ar" ? "ar-SA" : "en-US";

interface CheckoutStrings {
  // Header
  title: string;
  subtitle: string;
  switchLanguage: string;

  // Member picker
  memberSectionTitle: string;
  memberSectionHint: string;
  memberPickerLoading: string;
  memberPickerEmpty: string;
  memberPickerError: string;
  retry: string;

  // Payment method tiles
  methodSectionTitle: string;
  methodSecurityNote: string;
  madaTitle: string;
  madaSubtitle: string;
  applePayTitle: string;
  applePaySubtitle: string;
  applePayUnavailable: string;
  visaTitle: string;
  visaSubtitle: string;
  comingSoon: string;
  methodHandoffNote: (providerLabel: string) => string;

  // Invoice
  invoiceTitle: string;
  invoiceForMember: (name: string, smfId: string) => string;
  invoiceNoMember: string;
  annualLicenseFee: string;
  annualLicenseFeeDescription: string;
  subtotalExclVat: string;
  vatLabel: (percent: string) => string;
  total: string;

  // Actions
  payNow: (amount: string, providerLabel: string) => string;
  redirecting: (providerLabel: string) => string;
  redirectFallback: string;
  cancel: string;

  // Legal
  legalDisclaimer: string;

  // Errors
  networkError: string;
  memberNotFound: string;
}

const en: CheckoutStrings = {
  title: "Checkout",
  subtitle:
    "Pay the annual license fee for an approved member. You'll be redirected to the provider's hosted checkout.",
  switchLanguage: "عربي",

  memberSectionTitle: "Member",
  memberSectionHint: "Only Approved members can pay.",
  memberPickerLoading: "Loading members…",
  memberPickerEmpty:
    "No approved members yet. Approve a registration in the Members tab first.",
  memberPickerError: "Failed to load members.",
  retry: "Retry",

  methodSectionTitle: "Payment method",
  methodSecurityNote: "All payments secured by 3-D Secure",
  madaTitle: "mada",
  madaSubtitle: "Saudi debit cards",
  applePayTitle: "Apple Pay",
  applePaySubtitle: "Pay with Face ID / Touch ID",
  applePayUnavailable: "Available on Safari / iOS",
  visaTitle: "Visa",
  visaSubtitle: "Credit or debit card",
  comingSoon: "Soon",
  methodHandoffNote: (providerLabel) =>
    `You'll be redirected to a secure ${providerLabel} checkout window to complete the payment.`,

  invoiceTitle: "Invoice summary",
  invoiceForMember: (name, smfId) => `For ${name} · ${smfId}`,
  invoiceNoMember: "Select an approved member to continue",
  annualLicenseFee: "Annual License Fee",
  annualLicenseFeeDescription: "SMF membership — 2026 season",
  subtotalExclVat: "Subtotal (excl. VAT)",
  vatLabel: (percent) => `VAT (${percent}%)`,
  total: "Total",

  payNow: (amount, providerLabel) => `Pay ${amount} with ${providerLabel}`,
  redirecting: (providerLabel) => `Redirecting to ${providerLabel}…`,
  redirectFallback: "Click here if nothing happens",
  cancel: "Cancel",

  legalDisclaimer:
    "By clicking \"Pay\" you agree to be redirected to the provider's hosted checkout. SMF never stores your card details — the acquirer returns a signed webhook once the payment is authorised.",

  networkError: "Network error.",
  memberNotFound: "Member not found. Refresh the list and try again.",
};

const ar: CheckoutStrings = {
  title: "الدفع",
  subtitle:
    "ادفع رسوم الترخيص السنوي لعضو موافَق عليه. سيتم تحويلك إلى صفحة الدفع الآمنة الخاصة بالمزوّد.",
  switchLanguage: "English",

  memberSectionTitle: "العضو",
  memberSectionHint: "يمكن للأعضاء الموافق عليهم فقط إتمام الدفع.",
  memberPickerLoading: "جاري تحميل الأعضاء…",
  memberPickerEmpty:
    "لا يوجد أعضاء موافق عليهم بعد. يرجى الموافقة على تسجيل عضو من قائمة الأعضاء أولاً.",
  memberPickerError: "تعذّر تحميل قائمة الأعضاء.",
  retry: "إعادة المحاولة",

  methodSectionTitle: "طريقة الدفع",
  methodSecurityNote: "جميع المدفوعات محمية بنظام 3-D Secure",
  madaTitle: "مدى",
  madaSubtitle: "بطاقات الخصم السعودية",
  applePayTitle: "Apple Pay",
  applePaySubtitle: "ادفع باستخدام Face ID أو Touch ID",
  applePayUnavailable: "متاح على Safari / iOS",
  visaTitle: "Visa",
  visaSubtitle: "بطاقة ائتمان أو خصم",
  comingSoon: "قريباً",
  methodHandoffNote: (providerLabel) =>
    `سيتم تحويلك إلى نافذة دفع آمنة عبر ${providerLabel} لإكمال العملية.`,

  invoiceTitle: "ملخص الفاتورة",
  invoiceForMember: (name, smfId) => `للعضو ${name} · ${smfId}`,
  invoiceNoMember: "اختر عضواً موافق عليه للمتابعة",
  annualLicenseFee: "رسوم الترخيص السنوي",
  annualLicenseFeeDescription: "عضوية الاتحاد السعودي للملاكمة التايلاندية — موسم 2026",
  subtotalExclVat: "المجموع (قبل الضريبة)",
  vatLabel: (percent) => `ضريبة القيمة المضافة (${percent}٪)`,
  total: "الإجمالي",

  payNow: (amount, providerLabel) => `ادفع ${amount} عبر ${providerLabel}`,
  redirecting: (providerLabel) => `جاري التحويل إلى ${providerLabel}…`,
  redirectFallback: "اضغط هنا إذا لم يحدث شيء",
  cancel: "إلغاء",

  legalDisclaimer:
    "بالضغط على \"ادفع\" فإنك توافق على التحويل إلى صفحة الدفع الخاصة بالمزوّد. لا يقوم الاتحاد بحفظ بيانات بطاقتك — يتلقى النظام إشعاراً موقّعاً رقمياً بمجرد اعتماد عملية الدفع.",

  networkError: "خطأ في الشبكة.",
  memberNotFound: "العضو غير موجود. يرجى تحديث القائمة والمحاولة مرة أخرى.",
};

const STRINGS: Record<Locale, CheckoutStrings> = { en, ar };

export type CheckoutT = CheckoutStrings;

export function stringsFor(locale: Locale): CheckoutT {
  return STRINGS[locale];
}
