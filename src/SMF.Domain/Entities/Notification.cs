using SMF.Domain.Enums;

namespace SMF.Domain.Entities;

/// <summary>
/// A single outbound message destined for one recipient on one channel. Acts
/// as the persistence layer in front of the per-channel sender abstractions
/// (email/SMS/push) so the federation has a queryable audit log of every
/// transactional message and broadcast fan-out.
///
/// Why persist this and not just call the provider inline?
///   * Compliance — KSA telecom rules + GDPR-style "evidence of delivery".
///   * Retry — failed sends keep a row with <see cref="LastError"/> so an
///     operator can re-queue them.
///   * Reporting — admins need delivered/failed counters per campaign.
/// </summary>
public class Notification
{
    public Guid Id { get; private set; }
    public NotificationChannel Channel { get; private set; }

    /// <summary>Email address, E.164 phone, or FCM device token, depending on <see cref="Channel"/>.</summary>
    public string RecipientAddress { get; private set; } = default!;

    /// <summary>Subject line (email/push title); ignored by SMS senders.</summary>
    public string Subject { get; private set; } = default!;

    /// <summary>Plain-text body. HTML emails ride on top of the same column;
    /// the sender decides whether to render it.</summary>
    public string Body { get; private set; } = default!;

    public NotificationStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }
    public string? ProviderMessageId { get; private set; }

    /// <summary>Optional — set when the recipient is a known federation member.
    /// Lets the admin "Notifications by member" view exist without a join hack.</summary>
    public Guid? MemberId { get; private set; }

    /// <summary>Optional — set when this notification is one shard of a fanned-out
    /// <see cref="BroadcastCampaign"/>. <c>null</c> for transactional sends.</summary>
    public Guid? BroadcastCampaignId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? SentAtUtc { get; private set; }

    private Notification() { }

    public static Notification Queue(
        NotificationChannel channel,
        string recipientAddress,
        string subject,
        string body,
        Guid? memberId,
        Guid? broadcastCampaignId,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(recipientAddress))
            throw new ArgumentException("Recipient required.", nameof(recipientAddress));
        if (subject is null) throw new ArgumentNullException(nameof(subject));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Body required.", nameof(body));

        return new Notification
        {
            Id                  = Guid.NewGuid(),
            Channel             = channel,
            RecipientAddress    = recipientAddress.Trim(),
            Subject             = subject.Trim(),
            Body                = body,
            Status              = NotificationStatus.Queued,
            AttemptCount        = 0,
            MemberId            = memberId == Guid.Empty ? null : memberId,
            BroadcastCampaignId = broadcastCampaignId == Guid.Empty ? null : broadcastCampaignId,
            CreatedAtUtc        = nowUtc
        };
    }

    public void MarkSent(string? providerMessageId, DateTime nowUtc)
    {
        Status            = NotificationStatus.Sent;
        ProviderMessageId = providerMessageId;
        LastError         = null;
        AttemptCount     += 1;
        SentAtUtc         = nowUtc;
    }

    public void MarkFailed(string error, DateTime nowUtc)
    {
        Status        = NotificationStatus.Failed;
        LastError     = error.Length > 1000 ? error[..1000] : error;
        AttemptCount += 1;
        SentAtUtc     = nowUtc;
    }
}
