namespace SMF.Infrastructure.Payments;

public sealed class PaymentGatewayOptions
{
    public const string SectionName = "Payments";

    /// <summary>
    /// Active provider binding. <c>"Mada"</c> uses the in-memory sandbox
    /// (<see cref="MadaPaymentGatewayService"/>); <c>"Tap"</c> wires
    /// <see cref="TapPaymentGatewayService"/> against the real Tap Payments
    /// API (<see cref="TapApiBaseUrl"/> + <see cref="TapSecretKey"/>).
    /// </summary>
    public string Provider { get; set; } = "Mada";

    /// <summary>
    /// Base URL the sandbox gateway returns in its redirect URL. Irrelevant
    /// outside the sandbox implementation — production acquirers return their
    /// own hosted checkout URL.
    /// </summary>
    public string SandboxCheckoutBaseUrl { get; set; } = "https://sandbox.smf.local/checkout";

    /// <summary>
    /// HMAC-SHA256 shared secret used to sign webhook bodies. The real
    /// acquirer will supply this through their dashboard; we store it as an
    /// opaque string and never expose it to clients.
    /// </summary>
    public string WebhookSigningSecret { get; set; } = string.Empty;

    /// <summary>
    /// Name of the request header the provider sets with the HMAC digest.
    /// MADA sandbox typically uses "X-Signature"; Tap Payments sends
    /// "hashstring" — keep both in sync with the active provider.
    /// </summary>
    public string WebhookSignatureHeader { get; set; } = "X-Smf-Signature";

    // ---- Tap Payments specific ---------------------------------------------

    /// <summary>Tap Payments base API URL. Default targets production; sandbox is the same root with sandbox keys.</summary>
    public string TapApiBaseUrl { get; set; } = "https://api.tap.company";

    /// <summary>
    /// Server-side secret API key issued by Tap (starts with <c>sk_test_</c>
    /// or <c>sk_live_</c>). Bearer-auth on every outgoing call. Never log.
    /// </summary>
    public string TapSecretKey { get; set; } = string.Empty;

    /// <summary>Public Tap "merchant" reference shown to the buyer on the hosted checkout page.</summary>
    public string? TapMerchantId { get; set; }

    /// <summary>The default redirect URL the customer lands on after they finish (or abort) the hosted checkout.</summary>
    public string TapPostUrl { get; set; } = "https://localhost:5173/checkout/result";
}

/// <summary>
/// Federation invoicing settings — issuer block + VAT default. Surfaced to
/// the Application layer through <see cref="SMF.Application.Common.Interfaces.IInvoicingSettings"/>.
/// </summary>
public sealed class InvoicingOptions
{
    public const string SectionName = "Invoicing";

    public string IssuerName { get; set; } = "Saudi MuayThai Federation";
    public string? IssuerTaxNumber { get; set; }
    public string? IssuerAddress { get; set; }

    /// <summary>VAT rate in basis points. KSA default is 1500 (15%).</summary>
    public int DefaultVatRateBp { get; set; } = 1500;

    public string DefaultCurrency { get; set; } = "SAR";

    /// <summary>
    /// Toggle real PDF rendering. When <c>true</c> the API serves
    /// <c>application/pdf</c> via QuestPDF; when <c>false</c> falls back to
    /// printable HTML.
    /// </summary>
    public bool EnableQuestPdfRenderer { get; set; }
}
