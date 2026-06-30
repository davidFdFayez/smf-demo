using Microsoft.AspNetCore.SignalR;
using SMF.Application.Features.Tournaments;

namespace SMF.Api.Hubs;

/// <summary>
/// Lightweight pub/sub hub for bracket subscribers. Anyone viewing a
/// tournament page joins the tournament's group; the backend broadcasts
/// <see cref="ITournamentClient.ReceiveBracketUpdate"/> every time a bout
/// result is recorded or the bracket is regenerated.
/// </summary>
public sealed class TournamentHub : Hub<ITournamentClient>
{
    public const string Path = "/hubs/tournaments";

    public Task JoinTournament(string tournamentId)
    {
        if (string.IsNullOrWhiteSpace(tournamentId))
            throw new HubException("tournamentId is required.");
        return Groups.AddToGroupAsync(Context.ConnectionId, tournamentId, Context.ConnectionAborted);
    }

    public Task LeaveTournament(string tournamentId)
    {
        if (string.IsNullOrWhiteSpace(tournamentId))
            throw new HubException("tournamentId is required.");
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, tournamentId, Context.ConnectionAborted);
    }
}

public interface ITournamentClient
{
    Task ReceiveBracketUpdate(TournamentDetails tournament);
}
