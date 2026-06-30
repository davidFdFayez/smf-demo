using SMF.Domain.Enums;

namespace SMF.Domain.Entities;

/// <summary>
/// Federation event (PDF §1 "Events" + §6 "Tournaments & Registration").
/// Named <c>FederationEvent</c> instead of <c>Event</c> to avoid confusion
/// with the <c>event</c> C# keyword in handler code. Owns its list of
/// registrations as a child collection so the whole registration list
/// commits transactionally with status changes on the event itself.
/// </summary>
public class FederationEvent
{
    public Guid Id { get; private set; }

    public string Title { get; private set; } = default!;
    public string? Description { get; private set; }
    public string Location { get; private set; } = default!;

    public DateTime StartsAtUtc { get; private set; }
    public DateTime EndsAtUtc { get; private set; }

    public DateTime RegistrationOpensAtUtc { get; private set; }
    public DateTime RegistrationClosesAtUtc { get; private set; }

    /// <summary>Registration fee in minor units (halalas for SAR). 0 = free.</summary>
    public long EntryFeeMinor { get; private set; }

    public string Currency { get; private set; } = "SAR";

    public int? Capacity { get; private set; }

    public EventStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    // Live streaming (PDF §9 "Advanced Features → Live streaming integration").
    // Optional — most events go up without a broadcast.
    public LiveStreamProvider? LiveStreamProvider { get; private set; }
    public string? LiveStreamUrl { get; private set; }

    private readonly List<EventRegistration> _registrations = new();
    public IReadOnlyCollection<EventRegistration> Registrations => _registrations.AsReadOnly();

    private FederationEvent() { }

    public static FederationEvent Draft(
        string title,
        string? description,
        string location,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        DateTime registrationOpensAtUtc,
        DateTime registrationClosesAtUtc,
        long entryFeeMinor,
        string currency,
        int? capacity,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(location))
            throw new ArgumentException("Location is required.", nameof(location));
        if (endsAtUtc <= startsAtUtc)
            throw new ArgumentException("Event end time must be after start time.", nameof(endsAtUtc));
        if (registrationClosesAtUtc <= registrationOpensAtUtc)
            throw new ArgumentException(
                "Registration close time must be after open time.", nameof(registrationClosesAtUtc));
        if (registrationClosesAtUtc > endsAtUtc)
            throw new ArgumentException(
                "Registration cannot close after the event has ended.", nameof(registrationClosesAtUtc));
        if (entryFeeMinor < 0)
            throw new ArgumentOutOfRangeException(nameof(entryFeeMinor), "Entry fee cannot be negative.");
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("Currency must be an ISO-4217 code.", nameof(currency));
        if (capacity is <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive (or null = unlimited).");

        return new FederationEvent
        {
            Id = Guid.NewGuid(),
            Title = title.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Location = location.Trim(),
            StartsAtUtc = startsAtUtc,
            EndsAtUtc = endsAtUtc,
            RegistrationOpensAtUtc = registrationOpensAtUtc,
            RegistrationClosesAtUtc = registrationClosesAtUtc,
            EntryFeeMinor = entryFeeMinor,
            Currency = currency.ToUpperInvariant(),
            Capacity = capacity,
            Status = EventStatus.Draft,
            CreatedAtUtc = nowUtc
        };
    }

    public void UpdateDetails(
        string title,
        string? description,
        string location,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        DateTime registrationOpensAtUtc,
        DateTime registrationClosesAtUtc,
        long entryFeeMinor,
        string currency,
        int? capacity)
    {
        if (Status is EventStatus.Completed or EventStatus.Cancelled)
            throw new InvalidOperationException(
                $"Event {Id} is {Status} and can no longer be edited.");
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(location))
            throw new ArgumentException("Location is required.", nameof(location));
        if (endsAtUtc <= startsAtUtc)
            throw new ArgumentException("Event end time must be after start time.", nameof(endsAtUtc));
        if (registrationClosesAtUtc <= registrationOpensAtUtc)
            throw new ArgumentException(
                "Registration close time must be after open time.", nameof(registrationClosesAtUtc));
        if (registrationClosesAtUtc > endsAtUtc)
            throw new ArgumentException(
                "Registration cannot close after the event has ended.", nameof(registrationClosesAtUtc));
        if (entryFeeMinor < 0)
            throw new ArgumentOutOfRangeException(nameof(entryFeeMinor), "Entry fee cannot be negative.");
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("Currency must be an ISO-4217 code.", nameof(currency));
        if (capacity is <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive (or null = unlimited).");

        // Don't shrink capacity below already-confirmed headcount — rejecting
        // the edit outright is kinder than silently bumping people out.
        if (capacity is { } cap)
        {
            var active = _registrations.Count(r => r.Status != EventRegistrationStatus.Cancelled);
            if (cap < active)
                throw new InvalidOperationException(
                    $"Cannot set capacity ({cap}) below current active registrations ({active}).");
        }

        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Location = location.Trim();
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
        RegistrationOpensAtUtc = registrationOpensAtUtc;
        RegistrationClosesAtUtc = registrationClosesAtUtc;
        EntryFeeMinor = entryFeeMinor;
        Currency = currency.ToUpperInvariant();
        Capacity = capacity;
    }

    public void Publish()
    {
        if (Status != EventStatus.Draft)
            throw new InvalidOperationException($"Event {Id} is not in Draft status.");
        Status = EventStatus.Published;
    }

    public void OpenRegistration() => Status = EventStatus.RegistrationOpen;
    public void CloseRegistration() => Status = EventStatus.RegistrationClosed;

    public void Start()
    {
        if (Status is EventStatus.Cancelled or EventStatus.Completed)
            throw new InvalidOperationException(
                $"Cannot start event {Id} from status {Status}.");
        Status = EventStatus.InProgress;
    }

    public void Complete() => Status = EventStatus.Completed;

    public void Cancel()
    {
        if (Status == EventStatus.Completed)
            throw new InvalidOperationException(
                $"Event {Id} is already completed and cannot be cancelled.");
        Status = EventStatus.Cancelled;
    }

    /// <summary>Attach a live-stream URL + provider. Pass <c>null</c> provider + url to clear.</summary>
    public void SetLiveStream(LiveStreamProvider? provider, string? url)
    {
        if (provider is null || string.IsNullOrWhiteSpace(url))
        {
            LiveStreamProvider = null;
            LiveStreamUrl = null;
            return;
        }
        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            throw new ArgumentException("Live stream URL must be absolute.", nameof(url));
        LiveStreamProvider = provider;
        LiveStreamUrl = url.Trim();
    }

    /// <summary>
    /// Registers a member. Enforces registration-window + capacity and makes
    /// sure a member can't double-register for the same event.
    /// </summary>
    public EventRegistration RegisterMember(Guid memberId, DateTime nowUtc)
    {
        if (memberId == Guid.Empty)
            throw new ArgumentException("Member id is required.", nameof(memberId));
        if (Status == EventStatus.Cancelled)
            throw new InvalidOperationException("Event has been cancelled.");
        if (nowUtc < RegistrationOpensAtUtc)
            throw new InvalidOperationException("Registration has not opened yet.");
        if (nowUtc > RegistrationClosesAtUtc)
            throw new InvalidOperationException("Registration has closed.");
        if (_registrations.Any(r =>
                r.MemberId == memberId &&
                r.Status != EventRegistrationStatus.Cancelled))
            throw new InvalidOperationException("Member is already registered for this event.");
        if (Capacity is { } cap &&
            _registrations.Count(r => r.Status != EventRegistrationStatus.Cancelled) >= cap)
            throw new InvalidOperationException("Event is at capacity.");

        var reg = EventRegistration.Create(Id, memberId, nowUtc);
        _registrations.Add(reg);
        return reg;
    }
}
