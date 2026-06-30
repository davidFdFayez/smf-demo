namespace SMF.Infrastructure.Persistence.Entities;

/// <summary>
/// Tracks the last-issued SMF_ID sequence number per year.
/// Updated atomically inside the same transaction that inserts a new Member
/// to guarantee uniqueness under concurrency.
/// </summary>
internal sealed class MemberSequence
{
    public int Year { get; set; }
    public int LastSequence { get; set; }

    // Optimistic concurrency token — EF will fail the update if another
    // transaction bumped the counter between read and write.
    // Initialized to an empty array so the InMemory provider (used by tests)
    // doesn't reject the insert; SQL Server ignores the value on insert and
    // substitutes its own server-generated rowversion (AfterSaveBehavior = Ignore).
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
