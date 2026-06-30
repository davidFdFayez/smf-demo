using SMF.Application.Features.Communication.Broadcasts;
using SMF.Domain.Entities;

namespace SMF.Application.Common.Interfaces;

/// <summary>
/// Coordinates fan-out of a single <see cref="BroadcastCampaign"/> across the
/// configured channels (email, SMS, push) onto persisted
/// <see cref="Notification"/> rows. Implementation lives in
/// <c>SMF.Infrastructure.Communication</c> so it can hold references to the
/// concrete per-channel senders.
/// </summary>
public interface INotificationDispatcher
{
    /// <summary>Send the campaign to the resolved recipient list. Updates
    /// counters on the campaign aggregate as deliveries succeed/fail and
    /// flushes <c>SaveChangesAsync</c> in batches.</summary>
    Task DispatchAsync(
        BroadcastCampaign campaign,
        IReadOnlyList<BroadcastRecipient> recipients,
        CancellationToken cancellationToken = default);

    /// <summary>One-off transactional send — used by the API endpoint that
    /// admins hit to send a single message without persisting a campaign
    /// aggregate (e.g. "test send"). Persists a <see cref="Notification"/>
    /// row as the audit trail.</summary>
    Task<Notification> SendDirectAsync(
        Domain.Enums.NotificationChannel channel,
        string recipientAddress,
        string subject,
        string body,
        Guid? memberId,
        CancellationToken cancellationToken = default);
}
