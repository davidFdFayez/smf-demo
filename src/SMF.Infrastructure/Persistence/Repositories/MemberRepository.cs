using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Entities;
using SMF.Infrastructure.Persistence.Entities;

namespace SMF.Infrastructure.Persistence.Repositories;

internal sealed class MemberRepository : IMemberRepository
{
    private const int MaxSequenceRetries = 5;

    private readonly ApplicationDbContext _db;

    public MemberRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(Member member, CancellationToken cancellationToken = default)
        => _db.Members.AddAsync(member, cancellationToken).AsTask();

    public Task<Member?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.Members.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public Task<Member?> GetBySmfIdAsync(string smfId, CancellationToken cancellationToken = default)
        => _db.Members.FirstOrDefaultAsync(m => m.SMF_ID == smfId, cancellationToken);

    public async Task<IReadOnlyList<Member>> ListAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var items = await _db.Members
            .AsNoTracking()
            .OrderByDescending(m => m.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return items;
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
        => _db.Members.CountAsync(cancellationToken);

    /// <summary>
    /// Allocates the next sequence number for the given year.
    /// Uses optimistic concurrency (RowVersion) with retry to stay safe across
    /// concurrent registrations — the first writer wins, losers retry with the fresh value.
    /// The increment is committed as part of SaveChangesAsync on the same DbContext,
    /// so the sequence and the Member insert share a single transaction.
    /// </summary>
    public async Task<int> GetNextSequenceForYearAsync(int year, CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < MaxSequenceRetries; attempt++)
        {
            var sequence = await _db.MemberSequences
                .FirstOrDefaultAsync(s => s.Year == year, cancellationToken);

            if (sequence is null)
            {
                sequence = new MemberSequence { Year = year, LastSequence = 1 };
                _db.MemberSequences.Add(sequence);

                try
                {
                    // Persist the new row immediately so a unique-key violation on the
                    // (Year) PK is surfaced as a concurrency issue we can retry.
                    await _db.SaveChangesAsync(cancellationToken);
                    return sequence.LastSequence;
                }
                catch (DbUpdateException)
                {
                    _db.Entry(sequence).State = EntityState.Detached;
                    continue;
                }
            }

            sequence.LastSequence += 1;

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                return sequence.LastSequence;
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another transaction bumped the counter first — reload and retry.
                await _db.Entry(sequence).ReloadAsync(cancellationToken);
            }
        }

        throw new InvalidOperationException(
            $"Unable to allocate SMF_ID sequence for year {year} after {MaxSequenceRetries} attempts.");
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}
