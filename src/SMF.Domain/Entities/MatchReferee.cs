namespace SMF.Domain.Entities;

/// <summary>
/// Owned entity linking a <see cref="Match"/> to an assigned referee.
/// </summary>
public class MatchReferee
{
    public Guid RefereeId { get; private set; }

    private MatchReferee() { }

    public MatchReferee(Guid refereeId)
    {
        if (refereeId == Guid.Empty)
            throw new ArgumentException("Referee id is required.", nameof(refereeId));
        RefereeId = refereeId;
    }
}
