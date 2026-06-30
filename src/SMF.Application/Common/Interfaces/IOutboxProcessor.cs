namespace SMF.Application.Common.Interfaces;

/// <summary>
/// Drains the outbox. Lives behind an interface so:
///   1. The BackgroundService can resolve it per scope.
///   2. Integration tests can invoke it deterministically after a
///      state-changing call instead of racing the poll loop.
/// </summary>
public interface IOutboxProcessor
{
    /// <summary>
    /// Processes a batch of pending outbox messages and returns the number
    /// of messages that were successfully dispatched on this call.
    /// </summary>
    Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default);
}
