using SMF.Domain.Entities;

namespace SMF.Application.Common.Interfaces;

/// <summary>
/// Append-only store for scoring events. Writes must be durable before
/// broadcasting so a reconnecting scoreboard can replay a match.
/// </summary>
public interface IScoringEventStore
{
    Task RecordStrikeAsync(StrikeEvent strike, CancellationToken cancellationToken = default);

    Task RecordOverrideAsync(ScoreOverrideEvent scoreOverride, CancellationToken cancellationToken = default);

    Task RecordTimerAsync(RoundTimerEvent timerEvent, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StrikeEvent>> GetStrikesAsync(Guid matchId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScoreOverrideEvent>> GetOverridesAsync(Guid matchId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoundTimerEvent>> GetTimerEventsAsync(Guid matchId, CancellationToken cancellationToken = default);
}
