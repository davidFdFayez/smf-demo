using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace SMF.Infrastructure.Payments;

/// <summary>
/// HMAC-SHA256 verifier for inbound webhook bodies. The provider posts the
/// raw JSON body plus a header (configurable via <see cref="PaymentGatewayOptions.WebhookSignatureHeader"/>)
/// containing the hex digest of the body keyed by the shared secret. We
/// recompute, constant-time compare, and only let verified deliveries reach
/// the command handler.
/// </summary>
public sealed class WebhookSignatureVerifier
{
    private readonly IOptionsMonitor<PaymentGatewayOptions> _options;

    public WebhookSignatureVerifier(IOptionsMonitor<PaymentGatewayOptions> options)
    {
        _options = options;
    }

    public string HeaderName => _options.CurrentValue.WebhookSignatureHeader;

    public bool Verify(string rawBody, string? providedSignature)
    {
        if (string.IsNullOrWhiteSpace(providedSignature)) return false;

        var secret = _options.CurrentValue.WebhookSigningSecret;
        if (string.IsNullOrWhiteSpace(secret)) return false;

        var expected = Compute(rawBody, secret);

        // Constant-time compare to avoid leaking hints through timing.
        var a = Encoding.UTF8.GetBytes(expected);
        var b = Encoding.UTF8.GetBytes(providedSignature.Trim());
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    public static string Compute(string rawBody, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var payload = Encoding.UTF8.GetBytes(rawBody);
        var hash = HMACSHA256.HashData(key, payload);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
