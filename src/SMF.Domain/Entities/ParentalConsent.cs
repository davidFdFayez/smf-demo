using SMF.Domain.Enums;

namespace SMF.Domain.Entities;

/// <summary>
/// Parental-consent ceremony tied to a single minor athlete's registration.
/// Issued the moment the registration form posts with a DOB &lt; 18; sealed
/// either by the guardian opening the secure email link and signing, or by
/// the link expiring. The signed tuple (token-hash + timestamp + IP +
/// signature HMAC) lives on this row for forensic purposes; finer-grained
/// touchpoints (link opened, declined etc.) live in <see cref="ConsentLog"/>.
/// </summary>
public class ParentalConsent
{
    public Guid Id { get; private set; }
    public Guid MemberId { get; private set; }
    public ParentalConsentStatus Status { get; private set; }

    public string GuardianFullName { get; private set; } = default!;
    public GuardianRelation Relation { get; private set; }
    public string GuardianEmail { get; private set; } = default!;
    public string GuardianPhone { get; private set; } = default!;
    public string? GuardianNationalId { get; private set; }

    /// <summary>SHA-256 of the secure URL token issued to the guardian.
    /// We never store the raw token — only its hash — so a database leak
    /// can't be replayed to forge a signature.</summary>
    public string TokenHash { get; private set; } = default!;
    public DateTime TokenExpiresAtUtc { get; private set; }

    /// <summary>FK to the exact policy version (Code of Conduct / Privacy)
    /// the guardian saw when signing. Populated at issue time so editors
    /// can rotate text without breaking historical audit.</summary>
    public Guid? PolicyDocumentId { get; private set; }
    public string? PolicyVersion { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? OpenedAtUtc { get; private set; }
    public DateTime? DecidedAtUtc { get; private set; }
    public string? DecisionIpAddress { get; private set; }
    public string? DecisionUserAgent { get; private set; }

    /// <summary>HMAC-SHA256 over (consent-id, member-id, decision,
    /// timestamp). Stored only on Approve/Decline so verifiers can
    /// reconstruct the signature offline from the persisted columns.</summary>
    public string? DecisionSignatureHmac { get; private set; }

    public string? DeclineReason { get; private set; }

    private ParentalConsent() { }

    public static ParentalConsent Issue(
        Guid memberId,
        string guardianFullName,
        GuardianRelation relation,
        string guardianEmail,
        string guardianPhone,
        string? guardianNationalId,
        string tokenHash,
        DateTime tokenExpiresAtUtc,
        Guid? policyDocumentId,
        string? policyVersion)
    {
        if (memberId == Guid.Empty)
            throw new ArgumentException("Member id required.", nameof(memberId));
        if (string.IsNullOrWhiteSpace(guardianFullName))
            throw new ArgumentException("Guardian name required.", nameof(guardianFullName));
        if (string.IsNullOrWhiteSpace(guardianEmail))
            throw new ArgumentException("Guardian email required.", nameof(guardianEmail));
        if (string.IsNullOrWhiteSpace(guardianPhone))
            throw new ArgumentException("Guardian phone required.", nameof(guardianPhone));
        if (string.IsNullOrWhiteSpace(tokenHash) || tokenHash.Length != 64)
            throw new ArgumentException("Token hash must be 64 hex chars.", nameof(tokenHash));
        if (tokenExpiresAtUtc <= DateTime.UtcNow)
            throw new ArgumentException("Token expiry must be in the future.", nameof(tokenExpiresAtUtc));

        return new ParentalConsent
        {
            Id = Guid.NewGuid(),
            MemberId = memberId,
            Status = ParentalConsentStatus.Pending,
            GuardianFullName = guardianFullName.Trim(),
            Relation = relation,
            GuardianEmail = guardianEmail.Trim(),
            GuardianPhone = guardianPhone.Trim(),
            GuardianNationalId = string.IsNullOrWhiteSpace(guardianNationalId) ? null : guardianNationalId.Trim(),
            TokenHash = tokenHash.ToLowerInvariant(),
            TokenExpiresAtUtc = tokenExpiresAtUtc,
            PolicyDocumentId = policyDocumentId,
            PolicyVersion = policyVersion,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void MarkOpened()
    {
        if (Status != ParentalConsentStatus.Pending) return;
        OpenedAtUtc ??= DateTime.UtcNow;
    }

    public void Approve(string? ipAddress, string? userAgent, string signatureHmac)
    {
        EnsureStillSignable();
        if (string.IsNullOrWhiteSpace(signatureHmac))
            throw new ArgumentException("Signature HMAC required.", nameof(signatureHmac));

        Status = ParentalConsentStatus.Approved;
        DecidedAtUtc = DateTime.UtcNow;
        DecisionIpAddress = ipAddress;
        DecisionUserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent[..Math.Min(userAgent.Length, 400)];
        DecisionSignatureHmac = signatureHmac;
    }

    public void Decline(string reason, string? ipAddress, string? userAgent, string signatureHmac)
    {
        EnsureStillSignable();
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Decline reason required.", nameof(reason));
        if (string.IsNullOrWhiteSpace(signatureHmac))
            throw new ArgumentException("Signature HMAC required.", nameof(signatureHmac));

        Status = ParentalConsentStatus.Declined;
        DecidedAtUtc = DateTime.UtcNow;
        DecisionIpAddress = ipAddress;
        DecisionUserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent[..Math.Min(userAgent.Length, 400)];
        DecisionSignatureHmac = signatureHmac;
        DeclineReason = reason.Trim();
    }

    public void Expire()
    {
        if (Status != ParentalConsentStatus.Pending) return;
        Status = ParentalConsentStatus.Expired;
        DecidedAtUtc ??= DateTime.UtcNow;
    }

    public void Revoke()
    {
        if (Status != ParentalConsentStatus.Approved)
            throw new InvalidOperationException(
                $"Cannot revoke a consent in status {Status}; only Approved consents can be revoked.");
        Status = ParentalConsentStatus.Revoked;
    }

    private void EnsureStillSignable()
    {
        if (Status != ParentalConsentStatus.Pending)
            throw new InvalidOperationException(
                $"Consent {Id} is in status {Status} — cannot be signed.");
        if (DateTime.UtcNow > TokenExpiresAtUtc)
            throw new InvalidOperationException(
                $"Consent {Id} link expired at {TokenExpiresAtUtc:O}.");
    }
}
