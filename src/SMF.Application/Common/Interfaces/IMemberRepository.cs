using SMF.Domain.Entities;

namespace SMF.Application.Common.Interfaces;

public interface IMemberRepository
{
    Task AddAsync(Member member, CancellationToken cancellationToken = default);

    Task<Member?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Member?> GetBySmfIdAsync(string smfId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Member>> ListAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the next sequence number for SMF_ID generation for the given year.
    /// Implementations must guarantee uniqueness per year (e.g. DB sequence / atomic increment).
    /// </summary>
    Task<int> GetNextSequenceForYearAsync(int year, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
