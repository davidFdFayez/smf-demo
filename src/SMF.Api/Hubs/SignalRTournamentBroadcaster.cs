using Microsoft.AspNetCore.SignalR;
using SMF.Application.Common.Interfaces;
using SMF.Application.Features.Tournaments;

namespace SMF.Api.Hubs;

/// <summary>
/// Concrete <see cref="ITournamentBroadcaster"/> that fans bracket updates
/// through the SignalR <see cref="TournamentHub"/>.
/// </summary>
public sealed class SignalRTournamentBroadcaster : ITournamentBroadcaster
{
    private readonly IHubContext<TournamentHub, ITournamentClient> _hub;

    public SignalRTournamentBroadcaster(IHubContext<TournamentHub, ITournamentClient> hub)
    {
        _hub = hub;
    }

    public Task BroadcastBracketUpdatedAsync(
        TournamentDetails tournament,
        CancellationToken cancellationToken = default)
    {
        return _hub.Clients
            .Group(tournament.Id.ToString())
            .ReceiveBracketUpdate(tournament);
    }
}
