using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Matches.Queries.ListMatches;

public sealed record MatchSummary(
    string Code,
    MatchStatus Status,
    DateTime ScheduledAtUtc,
    bool IsTimerRunning,
    int CurrentRound);

public sealed record ListMatchesQuery : IRequest<IReadOnlyList<MatchSummary>>;

public sealed class ListMatchesQueryHandler
    : IRequestHandler<ListMatchesQuery, IReadOnlyList<MatchSummary>>
{
    private readonly IApplicationDbContext _db;

    public ListMatchesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<MatchSummary>> Handle(ListMatchesQuery request, CancellationToken ct)
    {
        return await _db.Matches
            .AsNoTracking()
            .OrderByDescending(m => m.Status == MatchStatus.Live)
            .ThenByDescending(m => m.IsTimerRunning)
            .ThenByDescending(m => m.ScheduledAtUtc)
            .ThenBy(m => m.Code)
            .Select(m => new MatchSummary(
                m.Code,
                m.Status,
                m.ScheduledAtUtc,
                m.IsTimerRunning,
                m.CurrentRound))
            .ToListAsync(ct);
    }
}
