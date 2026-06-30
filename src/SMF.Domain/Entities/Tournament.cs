using SMF.Domain.Enums;

namespace SMF.Domain.Entities;

/// <summary>
/// Single-elimination tournament bracket for a <see cref="FederationEvent"/>
/// (PDF §5). Owns its <see cref="BracketMatch"/> children. The bracket is
/// generated from a supplied seed list at creation time, then advanced by
/// calling <see cref="RecordResult"/> or <see cref="MarkNoShow"/> as matches
/// complete.
/// </summary>
public class Tournament
{
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public string Title { get; private set; } = default!;
    public string Division { get; private set; } = default!;
    public TournamentStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private readonly List<BracketMatch> _matches = new();
    public IReadOnlyCollection<BracketMatch> Matches => _matches.AsReadOnly();

    private Tournament() { }

    /// <summary>
    /// Builds a single-elimination bracket from the supplied seed list. The
    /// list is padded with BYE slots (represented as <c>null</c> participants)
    /// up to the next power of two so top seeds auto-advance through round 1
    /// when the participant count isn't a clean power of two. This is the
    /// standard IFMA seeding pattern.
    /// </summary>
    public static Tournament Generate(
        Guid eventId,
        string title,
        string division,
        IReadOnlyList<Guid> seededMemberIds,
        DateTime nowUtc)
    {
        if (eventId == Guid.Empty)
            throw new ArgumentException("EventId is required.", nameof(eventId));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(division))
            throw new ArgumentException("Division is required.", nameof(division));
        if (seededMemberIds.Count < 2)
            throw new ArgumentException(
                "A tournament needs at least two participants.", nameof(seededMemberIds));
        if (seededMemberIds.Distinct().Count() != seededMemberIds.Count)
            throw new ArgumentException(
                "Participants must be unique.", nameof(seededMemberIds));

        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Title = title.Trim(),
            Division = division.Trim(),
            Status = TournamentStatus.Draft,
            CreatedAtUtc = nowUtc
        };

        var bracketSize = NextPowerOfTwo(seededMemberIds.Count);
        var rounds = (int)Math.Log2(bracketSize);

        // Pad with BYEs to reach a power-of-two bracket.
        var seats = new Guid?[bracketSize];
        for (var i = 0; i < seededMemberIds.Count; i++) seats[i] = seededMemberIds[i];

        // Round 1 — materialise all first-round matches. A match with one
        // BYE slot is auto-walked-over to the real participant immediately
        // so they appear as the R2 participant without admin intervention.
        var round = 1;
        var orderInRound = 0;
        var currentRoundMatches = new List<BracketMatch>();

        for (var i = 0; i < bracketSize; i += 2)
        {
            var a = seats[i];
            var b = seats[i + 1];
            var match = BracketMatch.Create(tournament.Id, round, orderInRound++, a, b);

            if (a is null && b is null)
                match.MarkNoShowBoth();
            else if (a is null) match.Walkover(winnerIsA: false);
            else if (b is null) match.Walkover(winnerIsA: true);

            currentRoundMatches.Add(match);
            tournament._matches.Add(match);
        }

        // Subsequent rounds — create empty matches whose participants will be
        // filled in as predecessor matches complete. Store the predecessor
        // references so advancement is a simple set-participant op.
        for (round = 2; round <= rounds; round++)
        {
            var nextRound = new List<BracketMatch>();
            orderInRound = 0;

            for (var i = 0; i < currentRoundMatches.Count; i += 2)
            {
                var parentA = currentRoundMatches[i];
                var parentB = currentRoundMatches[i + 1];
                var match = BracketMatch.CreateEmpty(
                    tournament.Id,
                    round,
                    orderInRound++,
                    parentA.Id,
                    parentB.Id);
                nextRound.Add(match);
                tournament._matches.Add(match);
            }

            // Propagate walkover winners immediately so the bracket renders
            // correctly even before the first real match is fought.
            foreach (var m in nextRound)
            {
                TryPropagate(tournament, m);
            }

            currentRoundMatches = nextRound;
        }

        return tournament;
    }

    public void Start()
    {
        if (Status != TournamentStatus.Draft)
            throw new InvalidOperationException($"Tournament {Id} is not in Draft.");
        Status = TournamentStatus.InProgress;
    }

    public void RecordResult(Guid matchId, bool winnerIsA)
    {
        var match = _matches.FirstOrDefault(m => m.Id == matchId)
            ?? throw new InvalidOperationException(
                $"BracketMatch {matchId} is not part of this tournament.");

        match.Complete(winnerIsA);
        PropagateFromParent(match);

        if (_matches.All(m => m.IsTerminal()))
            Status = TournamentStatus.Completed;
    }

    /// <summary>
    /// Marks a scheduled bracket match as both participants being no-shows,
    /// so the bracket doesn't stall. The successor slot is left empty (the
    /// admin re-shuffles or awards the round by forfeit in a follow-up call).
    /// </summary>
    public void MarkNoShow(Guid matchId, bool? walkoverTo)
    {
        var match = _matches.FirstOrDefault(m => m.Id == matchId)
            ?? throw new InvalidOperationException(
                $"BracketMatch {matchId} is not part of this tournament.");

        if (walkoverTo is { } winnerIsA)
        {
            match.Walkover(winnerIsA);
            PropagateFromParent(match);
        }
        else
        {
            match.MarkNoShowBoth();
        }
    }

    private void PropagateFromParent(BracketMatch completed)
    {
        var child = _matches.FirstOrDefault(m =>
            m.ParentMatchAId == completed.Id || m.ParentMatchBId == completed.Id);
        if (child is null) return;
        TryPropagate(this, child);
    }

    private static void TryPropagate(Tournament t, BracketMatch child)
    {
        Guid? winnerFromA = child.ParentMatchAId is { } pA
            ? t._matches.First(m => m.Id == pA).Winner
            : null;

        Guid? winnerFromB = child.ParentMatchBId is { } pB
            ? t._matches.First(m => m.Id == pB).Winner
            : null;

        child.SetParticipants(winnerFromA, winnerFromB);
    }

    private static int NextPowerOfTwo(int n)
    {
        var pow = 1;
        while (pow < n) pow <<= 1;
        return pow;
    }
}
