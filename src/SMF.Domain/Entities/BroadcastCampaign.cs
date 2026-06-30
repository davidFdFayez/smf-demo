using SMF.Domain.Enums;

namespace SMF.Domain.Entities;

/// <summary>
/// Admin-authored campaign that fans out to a filtered slice of the membership
/// across the configured <see cref="Channels"/>. The targeting fields encode
/// the audience filter (role, club, event registration, status) — the
/// dispatcher resolves them to a concrete recipient list at send time so an
/// audience scheduled in advance always matches the membership state at the
/// moment of delivery, not the moment of creation.
/// </summary>
public class BroadcastCampaign
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = default!;
    public string Subject { get; private set; } = default!;
    public string Body { get; private set; } = default!;
    public BroadcastChannel Channels { get; private set; }

    // ── Targeting --------------------------------------------------------
    /// <summary>Role bitmap (encoded as a comma-separated list of
    /// <see cref="MemberRole"/> values; empty = all roles).</summary>
    public string? TargetRolesCsv { get; private set; }

    /// <summary>Restrict to a single club, e.g. "send to all members of Club X".</summary>
    public Guid? TargetClubId { get; private set; }

    /// <summary>Restrict to members registered for a specific event.</summary>
    public Guid? TargetEventId { get; private set; }

    /// <summary>If <c>true</c> only members in <see cref="RegistrationStatus.Active"/>
    /// receive the message; otherwise every status is included.</summary>
    public bool ActiveMembersOnly { get; private set; }

    // ── Lifecycle / counters --------------------------------------------
    public BroadcastStatus Status { get; private set; }
    public int TotalTargets { get; private set; }
    public int DeliveredCount { get; private set; }
    public int FailedCount { get; private set; }

    public Guid? CreatedByMemberId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ScheduledAtUtc { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string? FailureReason { get; private set; }

    private BroadcastCampaign() { }

    public static BroadcastCampaign Create(
        string title,
        string subject,
        string body,
        BroadcastChannel channels,
        IReadOnlyCollection<MemberRole>? targetRoles,
        Guid? targetClubId,
        Guid? targetEventId,
        bool activeMembersOnly,
        Guid? createdByMemberId,
        DateTime? scheduledAtUtc,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(title))   throw new ArgumentException("Title required.",   nameof(title));
        if (string.IsNullOrWhiteSpace(subject)) throw new ArgumentException("Subject required.", nameof(subject));
        if (string.IsNullOrWhiteSpace(body))    throw new ArgumentException("Body required.",    nameof(body));
        if ((channels & BroadcastChannel.All) == BroadcastChannel.None)
            throw new ArgumentException("At least one delivery channel must be selected.", nameof(channels));
        if (scheduledAtUtc is { } at && at < nowUtc.AddMinutes(-1))
            throw new ArgumentException("Scheduled time cannot be in the past.", nameof(scheduledAtUtc));

        return new BroadcastCampaign
        {
            Id                  = Guid.NewGuid(),
            Title               = title.Trim(),
            Subject             = subject.Trim(),
            Body                = body,
            Channels            = channels,
            TargetRolesCsv      = (targetRoles is null || targetRoles.Count == 0)
                                  ? null
                                  : string.Join(',', targetRoles.Select(r => (int)r)),
            TargetClubId        = targetClubId      == Guid.Empty ? null : targetClubId,
            TargetEventId       = targetEventId     == Guid.Empty ? null : targetEventId,
            ActiveMembersOnly   = activeMembersOnly,
            Status              = scheduledAtUtc is null ? BroadcastStatus.Draft : BroadcastStatus.Scheduled,
            CreatedByMemberId   = createdByMemberId == Guid.Empty ? null : createdByMemberId,
            CreatedAtUtc        = nowUtc,
            ScheduledAtUtc      = scheduledAtUtc
        };
    }

    public IReadOnlyCollection<MemberRole> ResolveTargetRoles()
    {
        if (string.IsNullOrWhiteSpace(TargetRolesCsv))
            return Array.Empty<MemberRole>();

        return TargetRolesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var v) ? (MemberRole?)v : null)
            .Where(r => r is not null)
            .Select(r => r!.Value)
            .Distinct()
            .ToArray();
    }

    public void MarkSending(int totalTargets, DateTime nowUtc)
    {
        if (Status is BroadcastStatus.Sent or BroadcastStatus.Failed or BroadcastStatus.Cancelled)
            throw new InvalidOperationException($"Cannot start a campaign in status {Status}.");
        Status        = BroadcastStatus.Sending;
        TotalTargets  = totalTargets;
        StartedAtUtc  = nowUtc;
    }

    public void RecordResult(bool delivered)
    {
        if (delivered) DeliveredCount += 1;
        else           FailedCount    += 1;
    }

    public void MarkComplete(DateTime nowUtc)
    {
        Status         = BroadcastStatus.Sent;
        CompletedAtUtc = nowUtc;
    }

    public void MarkFailed(string reason, DateTime nowUtc)
    {
        Status         = BroadcastStatus.Failed;
        FailureReason  = reason.Length > 500 ? reason[..500] : reason;
        CompletedAtUtc = nowUtc;
    }

    public void Cancel(DateTime nowUtc)
    {
        if (Status is BroadcastStatus.Sent or BroadcastStatus.Failed)
            throw new InvalidOperationException($"Cannot cancel a campaign in status {Status}.");
        Status         = BroadcastStatus.Cancelled;
        CompletedAtUtc = nowUtc;
    }
}
