using SMF.Domain.Enums;

namespace SMF.Domain.Entities;

/// <summary>
/// Single match within a <see cref="Tournament"/>. Designed to be reachable
/// strictly through the Tournament aggregate — the <c>internal</c> factories
/// and mutators enforce that advancement/no-show transitions always go
/// through the parent so bracket invariants stay intact.
/// </summary>
public class BracketMatch
{
    public Guid Id { get; private set; }
    public Guid TournamentId { get; private set; }
    public int Round { get; private set; }
    public int OrderInRound { get; private set; }

    public Guid? ParticipantAId { get; private set; }
    public Guid? ParticipantBId { get; private set; }

    public BracketMatchStatus Status { get; private set; }

    public Guid? Winner { get; private set; }

    /// <summary>Parent-match ids for rounds ≥ 2; null in round 1.</summary>
    public Guid? ParentMatchAId { get; private set; }
    public Guid? ParentMatchBId { get; private set; }

    private BracketMatch() { }

    internal static BracketMatch Create(
        Guid tournamentId,
        int round,
        int orderInRound,
        Guid? participantA,
        Guid? participantB) => new()
    {
        Id = Guid.NewGuid(),
        TournamentId = tournamentId,
        Round = round,
        OrderInRound = orderInRound,
        ParticipantAId = participantA,
        ParticipantBId = participantB,
        Status = BracketMatchStatus.Pending
    };

    internal static BracketMatch CreateEmpty(
        Guid tournamentId,
        int round,
        int orderInRound,
        Guid parentMatchA,
        Guid parentMatchB) => new()
    {
        Id = Guid.NewGuid(),
        TournamentId = tournamentId,
        Round = round,
        OrderInRound = orderInRound,
        ParentMatchAId = parentMatchA,
        ParentMatchBId = parentMatchB,
        Status = BracketMatchStatus.Pending
    };

    internal void SetParticipants(Guid? a, Guid? b)
    {
        if (Status is BracketMatchStatus.Completed or BracketMatchStatus.WalkoverA or BracketMatchStatus.WalkoverB)
            return;

        ParticipantAId = a;
        ParticipantBId = b;

        // Auto-resolve BYEs propagated from previous round.
        if (a is null && b is null) Status = BracketMatchStatus.NoShowBoth;
        else if (a is null) Walkover(winnerIsA: false);
        else if (b is null) Walkover(winnerIsA: true);
    }

    internal void Complete(bool winnerIsA)
    {
        if (ParticipantAId is null || ParticipantBId is null)
            throw new InvalidOperationException(
                $"BracketMatch {Id} does not have both participants yet.");

        Winner = winnerIsA ? ParticipantAId : ParticipantBId;
        Status = BracketMatchStatus.Completed;
    }

    internal void Walkover(bool winnerIsA)
    {
        if (winnerIsA)
        {
            Winner = ParticipantAId;
            Status = BracketMatchStatus.WalkoverA;
        }
        else
        {
            Winner = ParticipantBId;
            Status = BracketMatchStatus.WalkoverB;
        }
    }

    internal void MarkNoShowBoth()
    {
        Winner = null;
        Status = BracketMatchStatus.NoShowBoth;
    }

    public bool IsTerminal() => Status is
        BracketMatchStatus.Completed or
        BracketMatchStatus.WalkoverA or
        BracketMatchStatus.WalkoverB or
        BracketMatchStatus.NoShowBoth;
}
