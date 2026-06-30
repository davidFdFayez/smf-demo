namespace SMF.Infrastructure.DigitalId;

/// <summary>
/// Configuration for the offline Digital ID token (QR accreditation).
///
/// The signing secret is shared only between the API and the gate
/// scanner — never sent to the mobile client. This is what makes the
/// QR tamper-resistant: a forged token's HMAC won't match on the gate
/// side even if the attacker has unlimited offline attempts.
/// </summary>
public sealed class DigitalIdOptions
{
    public const string SectionName = "DigitalId";

    /// <summary>
    /// HMAC-SHA256 key. Minimum 32 bytes. Supply via user-secrets in
    /// dev and Key Vault (or equivalent) in production.
    /// </summary>
    public string SigningSecret { get; set; } = string.Empty;

    /// <summary>
    /// How long an issued token remains valid for admission. Longer =
    /// more useful for athletes with flaky connectivity; shorter =
    /// smaller forgery / sharing window. 24h is a sensible default for
    /// a single-day event; 72h for multi-day tournaments.
    /// </summary>
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Tolerance applied to <c>nbf</c>/<c>exp</c> checks so a minor
    /// clock skew between the mobile device, API server, and gate
    /// scanner never denies a legitimate athlete.
    /// </summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(2);
}
