using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Enums;

namespace SMF.Infrastructure.DigitalId;

/// <summary>
/// HMAC-SHA256 issuer/verifier for Digital ID tokens.
///
/// Wire format (JWT-lite, no JOSE header – we only ever speak one algo):
///   <c>base64url(payload_json) . base64url(hmac_sha256(payload, secret))</c>
///
/// Why not JWT? A real JOSE library would double the surface area for a
/// feature that never negotiates algorithms. Keeping this tiny and
/// fully-owned means the verify path on the gate scanner is ~30 lines.
/// </summary>
internal sealed class HmacDigitalIdTokenService : IDigitalIdTokenService
{
    private static readonly JsonSerializerOptions PayloadSerializer = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IOptionsMonitor<DigitalIdOptions> _options;
    private readonly IDateTimeProvider _clock;

    public HmacDigitalIdTokenService(
        IOptionsMonitor<DigitalIdOptions> options,
        IDateTimeProvider clock)
    {
        _options = options;
        _clock = clock;
    }

    public DigitalIdToken Issue(DigitalIdClaims claims)
    {
        var opts = _options.CurrentValue;
        EnsureSecretConfigured(opts);

        var now = _clock.UtcNow;
        var exp = now + opts.Lifetime;

        var payload = new DigitalIdPayload(
            Ver: 1,
            Sub: claims.MemberId,
            SmfId: claims.SmfId,
            FullName: claims.FullName,
            Status: claims.StatusAtIssue,
            Iat: ToUnixSeconds(now),
            Exp: ToUnixSeconds(exp));

        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, PayloadSerializer);
        var payloadB64 = Base64Url.Encode(payloadBytes);
        var signature = SignAscii(payloadB64, opts.SigningSecret);

        return new DigitalIdToken(
            Token: payloadB64 + "." + signature,
            IssuedAtUtc: now,
            ValidUntilUtc: exp);
    }

    public DigitalIdVerification Verify(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Reject(DigitalIdRejectionReason.Malformed);
        }

        var dot = token.IndexOf('.');
        if (dot <= 0 || dot == token.Length - 1)
        {
            return Reject(DigitalIdRejectionReason.Malformed);
        }

        var payloadB64 = token[..dot];
        var providedSig = token[(dot + 1)..];

        var opts = _options.CurrentValue;
        EnsureSecretConfigured(opts);

        var expectedSig = SignAscii(payloadB64, opts.SigningSecret);

        // Constant-time compare to avoid leaking timing hints.
        var a = Encoding.ASCII.GetBytes(expectedSig);
        var b = Encoding.ASCII.GetBytes(providedSig);
        if (a.Length != b.Length || !CryptographicOperations.FixedTimeEquals(a, b))
        {
            return Reject(DigitalIdRejectionReason.SignatureMismatch);
        }

        DigitalIdPayload? payload;
        try
        {
            var bytes = Base64Url.Decode(payloadB64);
            payload = JsonSerializer.Deserialize<DigitalIdPayload>(bytes, PayloadSerializer);
        }
        catch
        {
            return Reject(DigitalIdRejectionReason.Malformed);
        }

        if (payload is null || payload.Ver != 1 || string.IsNullOrEmpty(payload.SmfId))
        {
            return Reject(DigitalIdRejectionReason.Malformed);
        }

        var now = _clock.UtcNow;
        var skew = opts.ClockSkew;
        var iat = FromUnixSeconds(payload.Iat);
        var exp = FromUnixSeconds(payload.Exp);

        if (now + skew < iat)
        {
            return Reject(DigitalIdRejectionReason.NotYetValid, iat, exp);
        }
        if (now - skew > exp)
        {
            return Reject(DigitalIdRejectionReason.Expired, iat, exp);
        }

        var claims = new DigitalIdClaims(
            MemberId: payload.Sub,
            SmfId: payload.SmfId,
            FullName: payload.FullName,
            StatusAtIssue: payload.Status);

        return new DigitalIdVerification(
            IsValid: true,
            Reason: DigitalIdRejectionReason.None,
            Claims: claims,
            IssuedAtUtc: iat,
            ValidUntilUtc: exp);
    }

    private static DigitalIdVerification Reject(
        DigitalIdRejectionReason reason,
        DateTime? iat = null,
        DateTime? exp = null)
        => new(false, reason, null, iat, exp);

    private static string SignAscii(string payloadB64, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.ASCII.GetBytes(payloadB64);
        var hash = HMACSHA256.HashData(key, data);
        return Base64Url.Encode(hash);
    }

    private static long ToUnixSeconds(DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds();

    private static DateTime FromUnixSeconds(long seconds) =>
        DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;

    private static void EnsureSecretConfigured(DigitalIdOptions opts)
    {
        if (string.IsNullOrWhiteSpace(opts.SigningSecret) || opts.SigningSecret.Length < 32)
        {
            throw new InvalidOperationException(
                "DigitalId:SigningSecret is not configured (or is shorter than 32 chars). " +
                "Set it via user-secrets in dev and a secret store in production.");
        }
    }

    // Wire record — field names become the JSON keys via CamelCase.
    private sealed record DigitalIdPayload(
        int Ver,
        Guid Sub,
        string SmfId,
        string FullName,
        RegistrationStatus Status,
        long Iat,
        long Exp);
}

/// <summary>
/// Minimal RFC-4648 base64url codec. Avoids a NuGet dep just to trim the
/// two chars and strip padding.
/// </summary>
internal static class Base64Url
{
    public static string Encode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static byte[] Decode(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
            case 0: break;
            default: throw new FormatException("Invalid base64url length.");
        }
        return Convert.FromBase64String(s);
    }
}
