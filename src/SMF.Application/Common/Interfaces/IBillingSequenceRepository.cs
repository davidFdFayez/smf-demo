namespace SMF.Application.Common.Interfaces;

/// <summary>
/// Atomic, year-keyed sequence allocator for human-readable order and
/// invoice numbers. Backed by a single <c>BillingSequences</c> table with
/// optimistic concurrency (RowVersion + retry), mirroring how
/// <see cref="IMemberRepository"/> allocates SMF ids.
///
/// Keys are namespaced strings, e.g. <c>"ORDER:2026"</c>, <c>"INVOICE:2026"</c>.
/// </summary>
public interface IBillingSequenceRepository
{
    Task<int> NextAsync(string key, CancellationToken cancellationToken = default);
}
