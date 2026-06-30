using SMF.Domain.Enums;

namespace SMF.Domain.Entities;

/// <summary>
/// Append-only record of a member accepting a specific
/// <see cref="PolicyDocument"/> version. The combination of <c>MemberId</c>
/// + <c>PolicyKind</c> + <c>PolicyDocumentId</c> + <c>AcceptedAtUtc</c>
/// gives auditors a precise picture of which version was in force when
/// the user agreed.
/// </summary>
public class PolicyAcceptance
{
    public Guid Id { get; private set; }
    public Guid MemberId { get; private set; }
    public PolicyDocumentKind PolicyKind { get; private set; }

    /// <summary>FK to the exact <see cref="PolicyDocument"/> version that
    /// was on screen at acceptance time.</summary>
    public Guid PolicyDocumentId { get; private set; }

    /// <summary>Cached version string (from <see cref="PolicyDocument.Version"/>)
    /// so the audit log stays self-describing even if the document row is
    /// later renamed.</summary>
    public string PolicyVersion { get; private set; } = default!;

    public string ContentHash { get; private set; } = default!;
    public DateTime AcceptedAtUtc { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    /// <summary>Tamper-evident HMAC over the (member, policy, version,
    /// timestamp) tuple. Lets us detect retroactive edits to the
    /// audit table — if the HMAC no longer matches the row's columns,
    /// somebody bypassed the application.</summary>
    public string SignatureHmac { get; private set; } = default!;

    private PolicyAcceptance() { }

    public static PolicyAcceptance Record(
        Guid memberId,
        PolicyDocument policy,
        string? ipAddress,
        string? userAgent,
        string signatureHmac)
    {
        if (memberId == Guid.Empty)
            throw new ArgumentException("Member id is required.", nameof(memberId));
        if (policy is null)
            throw new ArgumentNullException(nameof(policy));
        if (string.IsNullOrWhiteSpace(signatureHmac))
            throw new ArgumentException("Signature HMAC is required.", nameof(signatureHmac));

        return new PolicyAcceptance
        {
            Id = Guid.NewGuid(),
            MemberId = memberId,
            PolicyKind = policy.Kind,
            PolicyDocumentId = policy.Id,
            PolicyVersion = policy.Version,
            ContentHash = policy.ContentHash,
            AcceptedAtUtc = DateTime.UtcNow,
            IpAddress = ipAddress,
            UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent[..Math.Min(userAgent.Length, 400)],
            SignatureHmac = signatureHmac
        };
    }
}
