namespace SMF.Domain.Enums;

/// <summary>
/// Discrete, append-only audit events written to <c>ConsentLogs</c>. Every
/// touchpoint of a parental-consent or policy-acceptance ceremony is logged
/// — issued, viewed, signed, declined, expired, revoked — so the federation
/// can reconstruct a tamper-evident trail for SOPC audits.
/// </summary>
public enum ConsentEventType
{
    PolicyAccepted     = 0,
    GuardianLinkIssued = 1,
    GuardianLinkOpened = 2,
    GuardianApproved   = 3,
    GuardianDeclined   = 4,
    LinkExpired        = 5,
    ConsentRevoked     = 6
}
