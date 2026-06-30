using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SMF.Application.Common.Interfaces;

namespace SMF.Infrastructure.Compliance;

/// <summary>
/// HMAC-SHA256 signer for consent payloads (<see cref="IConsentSigner"/>).
/// Pulls its secret from <see cref="ComplianceOptions.SigningSecret"/> via
/// <see cref="IOptionsMonitor{TOptions}"/> so the secret can be rotated
/// without restarting the host.
/// </summary>
public sealed class HmacConsentSigner : IConsentSigner
{
    private readonly IOptionsMonitor<ComplianceOptions> _options;

    public HmacConsentSigner(IOptionsMonitor<ComplianceOptions> options) => _options = options;

    public string Sign(string payload)
    {
        if (payload is null) throw new ArgumentNullException(nameof(payload));
        var key = Encoding.UTF8.GetBytes(_options.CurrentValue.SigningSecret);
        using var hmac = new HMACSHA256(key);
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public bool Verify(string payload, string signatureHex)
    {
        if (string.IsNullOrEmpty(signatureHex)) return false;
        var expected = Sign(payload);
        if (expected.Length != signatureHex.Length) return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(signatureHex.ToLowerInvariant()));
    }

    public string HashToken(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            throw new ArgumentException("Token must not be empty.", nameof(rawToken));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
