using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Entities;
using SMF.Infrastructure.Persistence;

namespace SMF.Infrastructure.Scoring;

internal sealed class EfScoringEventStore : IScoringEventStore
{
    private readonly ApplicationDbContext _db;

    public EfScoringEventStore(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task RecordStrikeAsync(StrikeEvent strike, CancellationToken cancellationToken = default)
    {
        await _db.StrikeEvents.AddAsync(strike, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordOverrideAsync(ScoreOverrideEvent scoreOverride, CancellationToken cancellationToken = default)
    {
        await _db.ScoreOverrideEvents.AddAsync(scoreOverride, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordTimerAsync(RoundTimerEvent timerEvent, CancellationToken cancellationToken = default)
    {
        await _db.RoundTimerEvents.AddAsync(timerEvent, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RoundTimerEvent>> GetTimerEventsAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        return await _db.RoundTimerEvents
            .AsNoTracking()
            .Where(e => e.MatchId == matchId)
            .OrderBy(e => e.OccurredAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StrikeEvent>> GetStrikesAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        return await _db.StrikeEvents
            .AsNoTracking()
            .Where(e => e.MatchId == matchId)
            .OrderBy(e => e.OccurredAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ScoreOverrideEvent>> GetOverridesAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        return await _db.ScoreOverrideEvents
            .AsNoTracking()
            .Where(e => e.MatchId == matchId)
            .OrderBy(e => e.OccurredAtUtc)
            .ToListAsync(cancellationToken);
    }
}
