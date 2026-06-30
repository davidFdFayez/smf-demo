namespace SMF.Domain.Enums;

/// <summary>
/// Per-message delivery status persisted on <see cref="Entities.Notification"/>.
/// Distinct from <see cref="BroadcastStatus"/> which tracks the campaign aggregate.
/// </summary>
public enum NotificationStatus
{
    Queued = 0,
    Sent   = 1,
    Failed = 2
}
