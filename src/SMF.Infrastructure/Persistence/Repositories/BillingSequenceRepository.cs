using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Interfaces;
using SMF.Infrastructure.Persistence.Entities;

namespace SMF.Infrastructure.Persistence.Repositories;

/// <summary>
/// Mirrors <see cref="MemberRepository.GetNextSequenceForYearAsync"/>: reads
/// the row, bumps the counter, persists with an optimistic concurrency
/// token, and retries up to <see cref="MaxSequenceRetries"/> times on
/// concurrent updates from sibling requests.
/// </summary>
internal sealed class BillingSequenceRepository : IBillingSequenceRepository
{
    private const int MaxSequenceRetries = 5;

    private readonly ApplicationDbContext _db;

    public BillingSequenceRepository(ApplicationDbContext db) => _db = db;

    public async Task<int> NextAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Sequence key is required.", nameof(key));

        for (var attempt = 0; attempt < MaxSequenceRetries; attempt++)
        {
            var sequence = await _db.BillingSequences
                .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

            if (sequence is null)
            {
                sequence = new BillingSequence { Key = key, LastSequence = 1 };
                _db.BillingSequences.Add(sequence);
                try
                {
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
                await _db.Entry(sequence).ReloadAsync(cancellationToken);
            }
        }

        throw new InvalidOperationException(
            $"Unable to allocate sequence for key '{key}' after {MaxSequenceRetries} attempts.");
    }
}
