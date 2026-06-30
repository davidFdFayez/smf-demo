using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMF.Application.Common.Interfaces;
using SMF.Infrastructure.Persistence;

namespace SMF.Infrastructure.Outbox;

/// <summary>
/// Drains a batch of pending outbox messages, dispatches each via MediatR,
/// and updates the row accordingly. Scoped: the caller supplies a fresh
/// ApplicationDbContext + IPublisher through DI on every invocation.
///
/// Concurrency note: on SQL Server, scaling this horizontally requires
/// SELECT ... WITH (UPDLOCK, READPAST) or an equivalent lease column so
/// two pollers don't dispatch the same row. Single-instance today; flagged
/// for follow-up before production scale-out.
/// </summary>
internal sealed class OutboxProcessor : IOutboxProcessor
{
    private readonly ApplicationDbContext _db;
    private readonly IPublisher _publisher;
    private readonly IDateTimeProvider _clock;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        ApplicationDbContext db,
        IPublisher publisher,
        IDateTimeProvider clock,
        IOptions<OutboxOptions> options,
        ILogger<OutboxProcessor> logger)
    {
        _db = db;
        _publisher = publisher;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var batch = await _db.OutboxMessages
            .Where(m => m.ProcessedAtUtc == null && m.Attempts < SMF.Domain.Entities.OutboxMessage.MaxAttempts)
            .OrderBy(m => m.OccurredAtUtc)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        if (batch.Count == 0)
            return 0;

        var dispatched = 0;

        foreach (var message in batch)
        {
            try
            {
                var clrType = Type.GetType(message.Type, throwOnError: false);
                if (clrType is null)
                {
                    message.RecordFailure(
                        $"Could not resolve CLR type '{message.Type}'.", _clock.UtcNow);
                    continue;
                }

                var notification = JsonSerializer.Deserialize(
                    message.Payload, clrType, EfOutbox.SerializerOptions) as INotification;

                if (notification is null)
                {
                    message.RecordFailure(
                        "Payload deserialised to null.", _clock.UtcNow);
                    continue;
                }

                await _publisher.Publish(notification, cancellationToken);
                message.MarkProcessed(_clock.UtcNow);
                dispatched += 1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to dispatch outbox message {MessageId} (type {Type}, attempt {Attempt}).",
                    message.Id, message.Type, message.Attempts + 1);
                message.RecordFailure(ex.Message, _clock.UtcNow);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return dispatched;
    }
}
