using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Persistence.Repositories;

internal sealed class MatchRepository : IMatchRepository
{
    private readonly ApplicationDbContext _db;

    public MatchRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(Match match, CancellationToken cancellationToken = default)
    {
        await _db.Matches.AddAsync(match, cancellationToken);
    }

    public Task<Match?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return _db.Matches
            .Include(m => m.Referees)
            .FirstOrDefaultAsync(m => m.Code == code, cancellationToken);
    }

    public Task<Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _db.Matches
            .Include(m => m.Referees)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}
