using SMF.Domain.Enums;

namespace SMF.Domain.Entities;

/// <summary>
/// Per-member registration into a <see cref="FederationEvent"/>. Child of the
/// event aggregate — construction, cancellation and check-in all go through
/// methods that mutate this entity (never a setter).
/// </summary>
public class EventRegistration
{
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid MemberId { get; private set; }
    public EventRegistrationStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? CheckedInAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }

    /// <summary>
    /// Set after successful fee payment. Free events leave this null.
    /// </summary>
    public Guid? PaymentId { get; private set; }

    private EventRegistration() { }

    internal static EventRegistration Create(Guid eventId, Guid memberId, DateTime nowUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            MemberId = memberId,
            Status = EventRegistrationStatus.Pending,
            CreatedAtUtc = nowUtc
        };

    public void Confirm(Guid? paymentId = null)
    {
        Status = EventRegistrationStatus.Confirmed;
        if (paymentId is { } p && p != Guid.Empty) PaymentId = p;
    }

    public void CheckIn(DateTime nowUtc)
    {
        if (Status == EventRegistrationStatus.Cancelled)
            throw new InvalidOperationException("Cancelled registrations cannot check in.");
        Status = EventRegistrationStatus.CheckedIn;
        CheckedInAtUtc = nowUtc;
    }

    public void MarkNoShow()
    {
        if (Status is EventRegistrationStatus.CheckedIn or EventRegistrationStatus.Cancelled)
            throw new InvalidOperationException(
                $"Cannot mark registration {Id} as no-show from status {Status}.");
        Status = EventRegistrationStatus.NoShow;
    }

    public void Cancel(DateTime nowUtc)
    {
        if (Status == EventRegistrationStatus.CheckedIn)
            throw new InvalidOperationException(
                "Cannot cancel a registration that has already checked in.");
        Status = EventRegistrationStatus.Cancelled;
        CancelledAtUtc = nowUtc;
    }
}
