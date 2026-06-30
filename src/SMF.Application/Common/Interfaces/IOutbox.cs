using MediatR;

namespace SMF.Application.Common.Interfaces;

/// <summary>
/// Writes domain notifications to the persistent outbox.
///
/// Contract: <see cref="EnqueueAsync"/> MUST NOT commit on its own. It only
/// attaches an OutboxMessage to the current unit of work. The caller commits
/// the aggregate and the outbox row together via a single SaveChangesAsync,
/// giving atomic "state changed ↔ event produced" semantics.
/// </summary>
public interface IOutbox
{
    Task EnqueueAsync<TNotification>(
        TNotification notification,
        CancellationToken cancellationToken = default)
        where TNotification : INotification;
}
