using SMF.Domain.Enums;

namespace SMF.Domain.Entities;

/// <summary>
/// Append-only record of a strike registered by a side referee.
/// Used for scoreboard replay and post-match audit.
/// </summary>
public class StrikeEvent
{
    public Guid Id { get; private set; }
    public Guid MatchId { get; private set; }
    public Guid RefereeId { get; private set; }
    public FighterColor FighterColor { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    private StrikeEvent() { }

    public static StrikeEvent Record(
        Guid matchId,
        Guid refereeId,
        FighterColor fighterColor,
        DateTime occurredAtUtc)
    {
        if (matchId == Guid.Empty) throw new ArgumentException("Match id is required.", nameof(matchId));
        if (refereeId == Guid.Empty) throw new ArgumentException("Referee id is required.", nameof(refereeId));

        return new StrikeEvent
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            RefereeId = refereeId,
            FighterColor = fighterColor,
            OccurredAtUtc = occurredAtUtc
        };
    }
}
