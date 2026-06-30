namespace SMF.Domain.Enums;

/// <summary>
/// Lifecycle of a guardian-consent request bound to a minor athlete's
/// registration. The "Pending" → "Approved" / "Declined" transitions are
/// driven by the guardian's interaction with the secure signing link;
/// "Expired" is set by a background sweep when the link's TTL elapses
/// without a decision.
/// </summary>
public enum ParentalConsentStatus
{
    Pending  = 0,
    Approved = 1,
    Declined = 2,
    Expired  = 3,
    Revoked  = 4
}
