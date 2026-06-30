using Microsoft.Extensions.Options;
using SMF.Application.Common.Interfaces;

namespace SMF.Infrastructure.Certificates;

/// <summary>
/// Builds the public certificate-verify URL from
/// <see cref="CertificateRenderingOptions.VerifyUrlTemplate"/>. Falls back
/// to the raw verification code when no template is configured so the
/// email body always contains *something* the recipient can paste into
/// support.
/// </summary>
internal sealed class CertificateVerifyUrlBuilder : ICertificateVerifyUrlBuilder
{
    private readonly IOptionsMonitor<CertificateRenderingOptions> _options;

    public CertificateVerifyUrlBuilder(IOptionsMonitor<CertificateRenderingOptions> options)
    {
        _options = options;
    }

    public string BuildVerifyUrl(string verificationCode)
    {
        var template = _options.CurrentValue.VerifyUrlTemplate;
        return string.IsNullOrWhiteSpace(template)
            ? verificationCode
            : template.Replace("{code}", Uri.EscapeDataString(verificationCode ?? string.Empty));
    }
}
