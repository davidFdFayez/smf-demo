using SMF.Application.Features.Tournaments;

namespace SMF.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the SignalR fan-out that pushes bracket updates to any
/// viewer subscribed to a tournament. Implemented in the API layer so the
/// Application project stays free of hub dependencies.
/// </summary>
public interface ITournamentBroadcaster
{
    Task BroadcastBracketUpdatedAsync(
        TournamentDetails tournament,
        CancellationToken cancellationToken = default);
}
