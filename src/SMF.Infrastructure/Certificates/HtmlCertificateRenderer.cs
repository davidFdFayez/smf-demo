using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using QRCoder;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Certificates;

/// <summary>
/// Renders a <see cref="Certificate"/> as a standalone HTML document
/// (UTF-8, self-contained CSS + inline SVG QR). Browsers can print the
/// document to PDF, which gets us functional certificate downloads without
/// pulling in a PDF dependency. A swap-in for QuestPDF or a headless-Chrome
/// renderer only needs to honour the same interface.
/// </summary>
internal sealed class HtmlCertificateRenderer : ICertificateRenderer
{
    private readonly IOptionsMonitor<CertificateRenderingOptions> _options;

    public HtmlCertificateRenderer(IOptionsMonitor<CertificateRenderingOptions> options)
    {
        _options = options;
    }

    public Task<CertificateDocument> RenderAsync(
        Certificate certificate, Member member, CancellationToken cancellationToken = default)
    {
        var verifyUrl = BuildVerifyUrl(certificate.VerificationCode);
        var qrSvg = BuildQrSvg(verifyUrl);
        var html = Build(certificate, member, verifyUrl, qrSvg);
        var bytes = Encoding.UTF8.GetBytes(html);
        var fileName = $"certificate-{certificate.VerificationCode}.html";
        return Task.FromResult(new CertificateDocument("text/html; charset=utf-8", bytes, fileName));
    }

    private string BuildVerifyUrl(string code)
    {
        var template = _options.CurrentValue.VerifyUrlTemplate;
        return string.IsNullOrWhiteSpace(template)
            ? code
            : template.Replace("{code}", Uri.EscapeDataString(code));
    }

    private static string BuildQrSvg(string payload)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        using var svg = new SvgQRCode(data);
        // 6 px per module, dark navy on transparent — meshes with certificate palette.
        return svg.GetGraphic(6, "#0c4a6e", "#ffffff");
    }

    private static string Build(Certificate c, Member m, string verifyUrl, string qrSvg)
    {
        static string Enc(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

        var issued = c.IssuedAtUtc.ToString("MMMM d, yyyy");
        var expires = c.ExpiresAtUtc.HasValue
            ? c.ExpiresAtUtc.Value.ToString("MMMM d, yyyy")
            : "No expiry";
        var status = c.IsRevoked ? "REVOKED" : "VALID";
        var statusColor = c.IsRevoked ? "#b91c1c" : "#0e7490";

        return $$"""
                 <!doctype html>
                 <html lang="en">
                 <head>
                   <meta charset="utf-8">
                   <title>{{Enc(c.Title)}} — {{Enc(m.FullName)}}</title>
                   <style>
                     @page { size: A4 landscape; margin: 0; }
                     body  { margin: 0; padding: 0; font-family: "Georgia", serif; color: #1e293b; }
                     .cert { width: 29.7cm; height: 21cm; box-sizing: border-box; padding: 2.5cm;
                             background: linear-gradient(135deg, #f8fafc 0%, #e0f2fe 100%);
                             border: 12px solid #0ea5e9; position: relative; }
                     .brand { position: absolute; top: 1cm; right: 1.5cm;
                              font-size: 14pt; font-weight: bold; color: #0c4a6e; letter-spacing: 0.15em; }
                     .status { position: absolute; top: 1cm; left: 1.5cm;
                               font-size: 11pt; font-weight: bold; color: {{statusColor}};
                               letter-spacing: 0.2em; border: 2px solid {{statusColor}};
                               padding: 0.1cm 0.4cm; border-radius: 0.2cm; }
                     h1    { text-align: center; margin-top: 2cm; font-size: 48pt; color: #0c4a6e;
                             letter-spacing: 0.05em; }
                     .sub  { text-align: center; font-size: 16pt; color: #334155; margin-top: -0.8cm; }
                     .name { text-align: center; font-size: 36pt; margin-top: 1cm; font-weight: bold;
                             color: #0f172a; }
                     .body { text-align: center; font-size: 14pt; line-height: 1.6; margin-top: 0.8cm;
                             max-width: 22cm; margin-left: auto; margin-right: auto; }
                     .meta { display: flex; justify-content: space-between; align-items: flex-end;
                             position: absolute; bottom: 2cm; left: 2.5cm; right: 2.5cm;
                             font-size: 10pt; color: #475569; }
                     .meta .box { border-top: 1px solid #94a3b8; padding-top: 0.3cm; min-width: 6cm; }
                     .meta .qr { text-align: right; }
                     .meta .qr svg { width: 3.2cm; height: 3.2cm; }
                     .code { font-family: "Courier New", monospace; font-size: 9pt; }
                     .url  { display: block; font-size: 8pt; color: #0c4a6e; margin-top: 0.15cm;
                             word-break: break-all; }
                   </style>
                 </head>
                 <body>
                   <div class="cert">
                     <div class="status">{{status}}</div>
                     <div class="brand">SAUDI MUAYTHAI FEDERATION</div>
                     <h1>Certificate</h1>
                     <div class="sub">of {{Enc(c.Type.ToString())}}</div>
                     <div class="name">{{Enc(m.FullName)}}</div>
                     <div class="body">
                       This certifies that the holder, SMF member <strong>{{Enc(m.SMF_ID)}}</strong>,
                       has been awarded &ldquo;{{Enc(c.Title)}}&rdquo;
                       {{(string.IsNullOrWhiteSpace(c.IssuingAuthority)
                         ? ""
                         : $"by <strong>{Enc(c.IssuingAuthority)}</strong>")}}.
                     </div>
                     <div class="meta">
                       <div class="box">Issued: {{issued}}<br>Valid until: {{expires}}</div>
                       <div class="box">Verification code:<br>
                         <span class="code">{{Enc(c.VerificationCode)}}</span>
                         <span class="url">{{Enc(verifyUrl)}}</span>
                       </div>
                       <div class="qr">{{qrSvg}}</div>
                     </div>
                   </div>
                 </body>
                 </html>
                 """;
    }
}
