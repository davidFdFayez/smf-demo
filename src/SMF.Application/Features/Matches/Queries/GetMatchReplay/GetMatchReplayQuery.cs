using MediatR;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Matches.Queries.GetMatchReplay;

/// <summary>
/// Returns the full append-only scoring event stream for a match so a
/// reconnecting scoreboard / dashboard can rebuild state without relying
/// on SignalR fan-out of past events.
/// </summary>
public sealed record GetMatchReplayQuery(string MatchCode) : IRequest<MatchReplayResult>;

public sealed record MatchReplayResult(
    Guid MatchId,
    string MatchCode,
    IReadOnlyList<StrikeEventDto> Strikes,
    IReadOnlyList<ScoreOverrideEventDto> Overrides);

public sealed record StrikeEventDto(
    Guid EventId,
    Guid RefereeId,
    FighterColor FighterColor,
    DateTimeOffset OccurredAtUtc);

public sealed record ScoreOverrideEventDto(
    Guid EventId,
    Guid HeadRefereeId,
    int Red,
    int Blue,
    int? Round,
    DateTimeOffset OccurredAtUtc);
