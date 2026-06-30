namespace SMF.Infrastructure.Persistence.Entities;

/// <summary>
/// Generic per-key sequence allocator backing <see cref="SMF.Application.Common.Interfaces.IBillingSequenceRepository"/>.
/// One row per (key) — typically <c>"ORDER:2026"</c>, <c>"INVOICE:2026"</c>.
/// Updated atomically inside the same SaveChanges batch that persists the
/// referencing aggregate, with optimistic concurrency on RowVersion.
/// </summary>
internal sealed class BillingSequence
{
    public string Key { get; set; } = default!;
    public int LastSequence { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
