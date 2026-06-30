using Microsoft.Extensions.Options;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Certificates;

/// <summary>
/// QuestPDF-backed certificate renderer. Produces a true binary PDF (A4
/// landscape, embedded QR code, deterministic layout) so the file the
/// member downloads or receives by email is the same artefact every
/// time — independent of the recipient's browser print stack.
/// </summary>
internal sealed class PdfCertificateRenderer : ICertificateRenderer
{
    private readonly IOptionsMonitor<CertificateRenderingOptions> _options;

    public PdfCertificateRenderer(IOptionsMonitor<CertificateRenderingOptions> options)
    {
        _options = options;
    }

    public Task<CertificateDocument> RenderAsync(
        Certificate certificate, Member member, CancellationToken cancellationToken = default)
    {
        var verifyUrl = BuildVerifyUrl(certificate.VerificationCode);
        var qrPng = BuildQrPng(verifyUrl);

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(0);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(t => t.FontFamily("Times New Roman").FontColor("#1e293b"));

                page.Content().Padding(36).Element(content =>
                {
                    content
                        .Border(8).BorderColor("#0ea5e9")
                        .Background(Colors.Grey.Lighten4)
                        .Padding(36).Column(col =>
                        {
                            col.Spacing(8);

                            col.Item().Row(header =>
                            {
                                header.RelativeItem().Text(certificate.IsRevoked ? "REVOKED" : "VALID")
                                    .FontSize(11).Bold()
                                    .FontColor(certificate.IsRevoked ? "#b91c1c" : "#0e7490")
                                    .LetterSpacing(0.2f);
                                header.RelativeItem().AlignRight()
                                    .Text("SAUDI MUAYTHAI FEDERATION")
                                    .FontSize(13).Bold().FontColor("#0c4a6e").LetterSpacing(0.18f);
                            });

                            col.Item().PaddingTop(50).AlignCenter()
                                .Text("Certificate")
                                .FontSize(56).Bold().FontColor("#0c4a6e").LetterSpacing(0.04f);

                            col.Item().AlignCenter()
                                .Text($"of {Pretty(certificate.Type.ToString())}")
                                .FontSize(16).FontColor("#334155");

                            col.Item().PaddingTop(24).AlignCenter()
                                .Text(member.FullName)
                                .FontSize(38).Bold().FontColor("#0f172a");

                            col.Item().PaddingTop(20).AlignCenter()
                                .MaxWidth(560)
                                .Text(text =>
                                {
                                    text.Span("This certifies that the holder, SMF member ");
                                    text.Span(member.SMF_ID).Bold();
                                    text.Span(", has been awarded ");
                                    text.Span($"\u201C{certificate.Title}\u201D").Italic();
                                    if (!string.IsNullOrWhiteSpace(certificate.IssuingAuthority))
                                    {
                                        text.Span(" by ");
                                        text.Span(certificate.IssuingAuthority).Bold();
                                    }
                                    text.Span(".");
                                });

                            col.Item().PaddingTop(48).Row(footer =>
                            {
                                footer.RelativeItem().Column(left =>
                                {
                                    left.Item().BorderTop(1).BorderColor("#94a3b8")
                                        .PaddingTop(6)
                                        .Text($"Issued: {certificate.IssuedAtUtc:MMMM d, yyyy}").FontSize(10);
                                    left.Item().Text(certificate.ExpiresAtUtc.HasValue
                                        ? $"Valid until: {certificate.ExpiresAtUtc:MMMM d, yyyy}"
                                        : "No expiry").FontSize(10);
                                });

                                footer.RelativeItem().Column(mid =>
                                {
                                    mid.Item().BorderTop(1).BorderColor("#94a3b8")
                                        .PaddingTop(6)
                                        .Text("Verification code:").FontSize(9).FontColor("#475569");
                                    mid.Item().Text(certificate.VerificationCode)
                                        .FontFamily("Courier New").FontSize(10);
                                    mid.Item().Text(verifyUrl)
                                        .FontSize(8).FontColor("#0c4a6e");
                                });

                                footer.ConstantItem(120).AlignRight().Image(qrPng);
                            });
                        });
                });
            });
        });

        var bytes = pdf.GeneratePdf();
        var fileName = $"certificate-{certificate.VerificationCode}.pdf";
        return Task.FromResult(new CertificateDocument("application/pdf", bytes, fileName));
    }

    private string BuildVerifyUrl(string code)
    {
        var template = _options.CurrentValue.VerifyUrlTemplate;
        return string.IsNullOrWhiteSpace(template)
            ? code
            : template.Replace("{code}", Uri.EscapeDataString(code));
    }

    private static byte[] BuildQrPng(string payload)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        using var png = new PngByteQRCode(data);
        return png.GetGraphic(8);
    }

    private static string Pretty(string camelOrPascal)
    {
        if (string.IsNullOrEmpty(camelOrPascal)) return camelOrPascal;
        var sb = new System.Text.StringBuilder(camelOrPascal.Length + 4);
        for (int i = 0; i < camelOrPascal.Length; i++)
        {
            var c = camelOrPascal[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(camelOrPascal[i - 1])) sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString();
    }
}
