namespace SMF.Application.Common.Interfaces;

/// <summary>
/// Authorizes and resolves a caller's relationship to a match in a single
/// round-trip. Returning <see cref="MatchAccess"/> in one call keeps the
/// scoring hub on the low-latency fast path (no extra DB hop for head-check).
/// </summary>
public interface IMatchAuthorizationService
{
    /// <summary>
    /// Resolves the match identified by <paramref name="matchCode"/> and the
    /// referee's access level. Returns <c>null</c> when the match does not exist.
    /// </summary>
    Task<MatchAccess?> GetAccessAsync(
        string matchCode,
        Guid refereeId,
        CancellationToken cancellationToken = default);
}

public sealed record MatchAccess(
    Guid MatchId,
    string MatchCode,
    bool IsAssigned,
    bool IsHeadReferee,
    bool IsTimekeeper = false);
