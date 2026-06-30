using Microsoft.Extensions.Options;
using SMF.Application.Features.Compliance.ParentalConsent;
using SMF.Infrastructure.Compliance;

namespace SMF.Api.Compliance;

/// <summary>
/// Builds the public guardian-signing URL the parental-consent email links
/// to. Prefers the configured <see cref="ComplianceOptions.PublicBaseUrl"/>
/// — falling back to the current request's host so dev environments work
/// without any extra config.
/// </summary>
public sealed class HttpComplianceUrlBuilder : IComplianceUrlBuilder
{
    private readonly IHttpContextAccessor _http;
    private readonly IOptionsMonitor<ComplianceOptions> _options;

    public HttpComplianceUrlBuilder(
        IHttpContextAccessor http, IOptionsMonitor<ComplianceOptions> options)
    {
        _http = http; _options = options;
    }

    public string BuildGuardianSigningUrl(Guid consentId, string token)
    {
        var configured = _options.CurrentValue.PublicBaseUrl?.TrimEnd('/');
        var baseUrl = !string.IsNullOrWhiteSpace(configured)
            ? configured
            : ResolveFromRequest();

        var encodedToken = Uri.EscapeDataString(token);
        return $"{baseUrl}/parental-consent/{consentId}/{encodedToken}";
    }

    private string ResolveFromRequest()
    {
        var ctx = _http.HttpContext;
        if (ctx is null) return "http://localhost:8080";
        return $"{ctx.Request.Scheme}://{ctx.Request.Host.Value}";
    }
}
