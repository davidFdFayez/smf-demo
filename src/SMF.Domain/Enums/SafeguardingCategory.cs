namespace SMF.Domain.Enums;

/// <summary>
/// Report types routed to the federation's safeguarding officer
/// (PDF §8 "Anti-Doping &amp; Safeguarding").
/// </summary>
public enum SafeguardingCategory
{
    AntiDoping = 1,
    Safeguarding = 2,
    Harassment = 3,
    MatchFixing = 4,
    Other = 5,
}

/// <summary>
/// Lifecycle of an incoming safeguarding report as it is triaged and closed.
/// </summary>
public enum SafeguardingReportStatus
{
    Submitted = 1,
    UnderReview = 2,
    Resolved = 3,
    Dismissed = 4,
}
