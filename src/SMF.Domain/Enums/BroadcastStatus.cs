namespace SMF.Domain.Enums;

/// <summary>Lifecycle states of a <see cref="Entities.BroadcastCampaign"/>.</summary>
public enum BroadcastStatus
{
    /// <summary>Created but not yet queued for delivery.</summary>
    Draft     = 0,
    /// <summary>Queued for the dispatcher to pick up at <c>ScheduledAtUtc</c>.</summary>
    Scheduled = 1,
    /// <summary>Dispatcher is actively fanning out per-recipient notifications.</summary>
    Sending   = 2,
    /// <summary>All targets reached terminal state. Inspect counters for partial failures.</summary>
    Sent      = 3,
    /// <summary>Dispatcher hard-failed (no recipients, provider outage, validation, etc.).</summary>
    Failed    = 4,
    /// <summary>Operator cancelled the campaign before it finished sending.</summary>
    Cancelled = 5
}
