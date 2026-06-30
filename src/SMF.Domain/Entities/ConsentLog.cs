using SMF.Domain.Enums;

namespace SMF.Domain.Entities;

/// <summary>
/// Append-only audit row covering every touchpoint of a consent ceremony
/// — link issued, link opened, signed, declined, expired, policy
/// accepted, consent revoked. Decoupled from <see cref="ParentalConsent"/>
/// and <see cref="PolicyAcceptance"/> so all events end up in one queryable
/// timeline regardless of which workflow produced them.
/// </summary>
public class ConsentLog
{
    public long Id { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public ConsentEventType EventType { get; private set; }

    public Guid? MemberId { get; private set; }
    public Guid? PolicyDocumentId { get; private set; }
    public Guid? ParentalConsentId { get; private set; }

    public string? PolicyKind { get; private set; }
    public string? PolicyVersion { get; private set; }
    public string? ContentHash { get; private set; }

    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    /// <summary>Free-form JSON details (decline reason, link target etc.).
    /// Bounded at the column level so a noisy actor can't blow up the
    /// table.</summary>
    public string? DetailsJson { get; private set; }

    /// <summary>Tamper-evident HMAC over the row's stable columns.
    /// Recomputed on read by the audit verifier; mismatches are alerted.</summary>
    public string? SignatureHmac { get; private set; }

    private ConsentLog() { }

    public static ConsentLog Record(
        ConsentEventType eventType,
        Guid? memberId,
        Guid? policyDocumentId,
        Guid? parentalConsentId,
        string? policyKind,
        string? policyVersion,
        string? contentHash,
        string? ipAddress,
        string? userAgent,
        string? detailsJson,
        string? signatureHmac)
    {
        return new ConsentLog
        {
            OccurredAtUtc = DateTime.UtcNow,
            EventType = eventType,
            MemberId = memberId,
            PolicyDocumentId = policyDocumentId,
            ParentalConsentId = parentalConsentId,
            PolicyKind = policyKind,
            PolicyVersion = policyVersion,
            ContentHash = contentHash,
            IpAddress = ipAddress,
            UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent[..Math.Min(userAgent.Length, 400)],
            DetailsJson = detailsJson,
            SignatureHmac = signatureHmac
        };
    }
}
