using SMF.Application.Features.Scoring;

namespace SMF.Api.Hubs;

/// <summary>
/// Strongly-typed contract for messages the server pushes to connected clients
/// (Head Referee Dashboards, Public Scoreboards, broadcast feeds).
/// </summary>
public interface IMatchScoringClient
{
    Task ReceiveStrikeUpdate(StrikeUpdatePayload payload);

    Task ReceiveScoreOverride(ScoreOverridePayload payload);

    /// <summary>Pushed by the timekeeper every time the round clock changes.</summary>
    Task ReceiveTimerUpdate(TimerStatePayload payload);
}
