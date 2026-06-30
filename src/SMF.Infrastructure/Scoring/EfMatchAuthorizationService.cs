using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Interfaces;
using SMF.Infrastructure.Persistence;

namespace SMF.Infrastructure.Scoring;

/// <summary>
/// EF Core-backed implementation. Returns the match id + authorization flags
/// in a single query so the hub fast-path stays at one round trip per call.
/// </summary>
internal sealed class EfMatchAuthorizationService : IMatchAuthorizationService
{
    private readonly ApplicationDbContext _db;

    public EfMatchAuthorizationService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<MatchAccess?> GetAccessAsync(
        string matchCode,
        Guid refereeId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(matchCode) || refereeId == Guid.Empty)
            return null;

        // Project only the fields we need — avoids hydrating owned collection
        // over the wire just to answer a boolean question.
        var access = await _db.Matches
            .AsNoTracking()
            .Where(m => m.Code == matchCode)
            .Select(m => new
            {
                m.Id,
                m.Code,
                IsHeadReferee = m.HeadRefereeId == refereeId,
                IsAssigned = m.HeadRefereeId == refereeId
                             || m.Referees.Any(r => r.RefereeId == refereeId),
                IsTimekeeper = m.TimekeeperId != null && m.TimekeeperId == refereeId
            })
            .FirstOrDefaultAsync(cancellationToken);

        return access is null
            ? null
            : new MatchAccess(
                access.Id, access.Code,
                access.IsAssigned, access.IsHeadReferee, access.IsTimekeeper);
    }
}
