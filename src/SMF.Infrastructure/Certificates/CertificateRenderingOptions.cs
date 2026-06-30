namespace SMF.Infrastructure.Certificates;

/// <summary>
/// Configurable rendering bits for <see cref="HtmlCertificateRenderer"/>:
/// the public certificate-verification URL template so the embedded QR code
/// resolves to the right page on the deployed web application.
/// </summary>
public sealed class CertificateRenderingOptions
{
    public const string SectionName = "Certificates";

    /// <summary>
    /// Public URL template where <c>{code}</c> is replaced by the
    /// certificate's verification code.
    /// Default: <c>http://localhost:5173/certificates/verify/{code}</c>.
    /// </summary>
    public string VerifyUrlTemplate { get; set; }
        = "http://localhost:5173/certificates/verify/{code}";

    /// <summary>
    /// When <c>true</c>, certificates are rendered as binary PDF via
    /// QuestPDF instead of the HTML fallback. Defaults to <c>true</c>
    /// in production-like environments; set to <c>false</c> to drop the
    /// native dependency for tiny/test deployments.
    /// </summary>
    public bool UsePdfRenderer { get; set; } = true;
}
