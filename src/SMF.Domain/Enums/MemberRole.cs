namespace SMF.Domain.Enums;

/// <summary>
/// All federation member roles. Mirrors PDF §3 "Role-Specific Registration":
/// athletes / coaches / referees are the three sport participants, while
/// <see cref="ClubAdmin"/>, <see cref="FederationStaff"/> and
/// <see cref="Visitor"/> cover the directory / administration / spectator
/// roles the federation also issues IDs for.
/// </summary>
public enum MemberRole
{
    Athlete = 1,
    Coach = 2,
    Referee = 3,
    ClubAdmin = 4,
    FederationStaff = 5,
    Visitor = 6
}
