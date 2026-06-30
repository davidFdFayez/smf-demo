using SMF.Domain.Entities;

namespace SMF.Application.Common.Interfaces;

public interface IMatchRepository
{
    Task AddAsync(Match match, CancellationToken cancellationToken = default);

    Task<Match?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
