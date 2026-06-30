using SMF.Domain.Enums;

namespace SMF.Domain.Entities;

/// <summary>
/// Append-only audit row for every round-timer transition. Paired with the
/// scoreboard strike/override log, a replay can reconstitute the full match
/// timeline deterministically.
/// </summary>
public class RoundTimerEvent
{
    public Guid Id { get; private set; }
    public Guid MatchId { get; private set; }
    public Guid TimekeeperId { get; private set; }
    public TimerAction Action { get; private set; }
    public int RoundNumber { get; private set; }
    public int RoundDurationSeconds { get; private set; }
    public int ElapsedSecondsAtEvent { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    private RoundTimerEvent() { }

    public static RoundTimerEvent Record(
        Guid matchId,
        Guid timekeeperId,
        TimerAction action,
        int roundNumber,
        int roundDurationSeconds,
        int elapsedSecondsAtEvent,
        DateTime occurredAtUtc)
    {
        if (matchId == Guid.Empty)
            throw new ArgumentException("Match id is required.", nameof(matchId));
        if (timekeeperId == Guid.Empty)
            throw new ArgumentException("Timekeeper id is required.", nameof(timekeeperId));
        if (roundNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(roundNumber), "Round must be 1+.");
        if (roundDurationSeconds < 1)
            throw new ArgumentOutOfRangeException(nameof(roundDurationSeconds),
                "Round duration must be at least one second.");
        if (elapsedSecondsAtEvent < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSecondsAtEvent),
                "Elapsed seconds cannot be negative.");

        return new RoundTimerEvent
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            TimekeeperId = timekeeperId,
            Action = action,
            RoundNumber = roundNumber,
            RoundDurationSeconds = roundDurationSeconds,
            ElapsedSecondsAtEvent = elapsedSecondsAtEvent,
            OccurredAtUtc = occurredAtUtc
        };
    }
}
