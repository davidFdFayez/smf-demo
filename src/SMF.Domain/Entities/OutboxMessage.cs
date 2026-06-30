namespace SMF.Domain.Entities;

/// <summary>
/// A persisted, not-yet-dispatched domain notification.
///
/// The outbox sits in the same database as the aggregates that produce it, so
/// committing the aggregate state change and enqueueing the notification share
/// a single SQL transaction. A background processor later deserialises the
/// payload and publishes it via MediatR. If the process crashes after the
/// commit but before the fan-out, the row stays pending and the next poll
/// picks it up — no event is lost.
/// </summary>
public class OutboxMessage
{
    public const int MaxAttempts = 10;

    public Guid Id { get; private set; }

    /// <summary>Assembly-qualified .NET type name of the notification.</summary>
    public string Type { get; private set; } = default!;

    /// <summary>JSON payload — round-trips back to <see cref="Type"/>.</summary>
    public string Payload { get; private set; } = default!;

    public DateTime OccurredAtUtc { get; private set; }

    public DateTime? ProcessedAtUtc { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    private OutboxMessage() { }

    private OutboxMessage(Guid id, string type, string payload, DateTime occurredAtUtc)
    {
        Id = id;
        Type = type;
        Payload = payload;
        OccurredAtUtc = occurredAtUtc;
        Attempts = 0;
    }

    public static OutboxMessage Create(string type, string payload, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Type is required.", nameof(type));
        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Payload is required.", nameof(payload));

        return new OutboxMessage(Guid.NewGuid(), type, payload, nowUtc);
    }

    public bool IsProcessed => ProcessedAtUtc is not null;

    public bool HasExceededMaxAttempts => Attempts >= MaxAttempts;

    public void MarkProcessed(DateTime nowUtc)
    {
        ProcessedAtUtc = nowUtc;
        LastError = null;
    }

    public void RecordFailure(string error, DateTime nowUtc)
    {
        Attempts += 1;
        LastError = string.IsNullOrWhiteSpace(error) ? "Unspecified error" : error;
        // NB: ProcessedAtUtc is deliberately left null so the row is picked
        // up again on the next poll until Attempts reaches MaxAttempts.
    }
}
