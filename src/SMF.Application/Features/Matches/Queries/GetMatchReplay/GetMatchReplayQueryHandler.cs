using MediatR;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;

namespace SMF.Application.Features.Matches.Queries.GetMatchReplay;

public sealed class GetMatchReplayQueryHandler
    : IRequestHandler<GetMatchReplayQuery, MatchReplayResult>
{
    private readonly IMatchRepository _matches;
    private readonly IScoringEventStore _events;

    public GetMatchReplayQueryHandler(IMatchRepository matches, IScoringEventStore events)
    {
        _matches = matches;
        _events = events;
    }

    public async Task<MatchReplayResult> Handle(GetMatchReplayQuery request, CancellationToken cancellationToken)
    {
        var match = await _matches.GetByCodeAsync(request.MatchCode, cancellationToken)
                    ?? throw new NotFoundException("Match", request.MatchCode);

        var strikes = await _events.GetStrikesAsync(match.Id, cancellationToken);
        var overrides = await _events.GetOverridesAsync(match.Id, cancellationToken);

        return new MatchReplayResult(
            match.Id,
            match.Code,
            strikes
                .Select(s => new StrikeEventDto(
                    s.Id,
                    s.RefereeId,
                    s.FighterColor,
                    new DateTimeOffset(DateTime.SpecifyKind(s.OccurredAtUtc, DateTimeKind.Utc))))
                .ToList(),
            overrides
                .Select(o => new ScoreOverrideEventDto(
                    o.Id,
                    o.HeadRefereeId,
                    o.Red,
                    o.Blue,
                    o.Round,
                    new DateTimeOffset(DateTime.SpecifyKind(o.OccurredAtUtc, DateTimeKind.Utc))))
                .ToList());
    }
}
