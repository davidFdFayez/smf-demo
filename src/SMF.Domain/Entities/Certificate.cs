using SMF.Domain.Enums;

namespace SMF.Domain.Entities;

/// <summary>
/// A certificate issued to a member (PDF §3 + §10). Stores only the metadata —
/// the actual PDF/HTML body is rendered on demand by the Infrastructure
/// generator so we can update the template without backfilling stored blobs.
/// </summary>
public class Certificate
{
    public Guid Id { get; private set; }
    public Guid MemberId { get; private set; }
    public CertificateType Type { get; private set; }
    public string Title { get; private set; } = default!;
    public string? IssuingAuthority { get; private set; }

    /// <summary>Short opaque code on the printed certificate ("SMF-CERT-XXXX"). Unique.</summary>
    public string VerificationCode { get; private set; } = default!;

    public DateTime IssuedAtUtc { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }

    public bool IsRevoked { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public string? RevocationReason { get; private set; }

    private Certificate() { }

    public static Certificate Issue(
        Guid memberId,
        CertificateType type,
        string title,
        string? issuingAuthority,
        DateTime issuedAtUtc,
        DateTime? expiresAtUtc)
    {
        if (memberId == Guid.Empty)
            throw new ArgumentException("MemberId is required.", nameof(memberId));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (expiresAtUtc is { } exp && exp <= issuedAtUtc)
            throw new ArgumentException(
                "Expiry must be after issue date.", nameof(expiresAtUtc));

        return new Certificate
        {
            Id = Guid.NewGuid(),
            MemberId = memberId,
            Type = type,
            Title = title.Trim(),
            IssuingAuthority = string.IsNullOrWhiteSpace(issuingAuthority) ? null : issuingAuthority.Trim(),
            VerificationCode = GenerateVerificationCode(),
            IssuedAtUtc = issuedAtUtc,
            ExpiresAtUtc = expiresAtUtc
        };
    }

    public void Revoke(string reason, DateTime nowUtc)
    {
        if (IsRevoked) return;
        IsRevoked = true;
        RevokedAtUtc = nowUtc;
        RevocationReason = string.IsNullOrWhiteSpace(reason) ? "Unspecified" : reason.Trim();
    }

    public bool IsValidOn(DateTime onUtc) =>
        !IsRevoked && (ExpiresAtUtc is null || ExpiresAtUtc > onUtc);

    private static string GenerateVerificationCode()
    {
        // Short unique-ish code for the printed cert. Full collision-avoidance
        // is enforced by a unique DB index; if we ever hit a collision the
        // caller should catch and retry.
        var guidPart = Guid.NewGuid().ToString("N").ToUpperInvariant()[..10];
        return $"SMF-CERT-{guidPart}";
    }
}
