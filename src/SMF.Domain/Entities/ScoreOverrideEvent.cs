namespace SMF.Domain.Entities;

/// <summary>
/// Append-only record of a head-referee scoreboard override.
/// </summary>
public class ScoreOverrideEvent
{
    public Guid Id { get; private set; }
    public Guid MatchId { get; private set; }
    public Guid HeadRefereeId { get; private set; }
    public int Red { get; private set; }
    public int Blue { get; private set; }
    public int? Round { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    private ScoreOverrideEvent() { }

    public static ScoreOverrideEvent Record(
        Guid matchId,
        Guid headRefereeId,
        int red,
        int blue,
        int? round,
        DateTime occurredAtUtc)
    {
        if (matchId == Guid.Empty) throw new ArgumentException("Match id is required.", nameof(matchId));
        if (headRefereeId == Guid.Empty) throw new ArgumentException("Head referee id is required.", nameof(headRefereeId));
        if (red < 0 || blue < 0) throw new ArgumentOutOfRangeException(nameof(red), "Scores must be non-negative.");
        if (round is { } r && r < 1) throw new ArgumentOutOfRangeException(nameof(round), "Round must be >= 1 when specified.");

        return new ScoreOverrideEvent
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            HeadRefereeId = headRefereeId,
            Red = red,
            Blue = blue,
            Round = round,
            OccurredAtUtc = occurredAtUtc
        };
    }
}
