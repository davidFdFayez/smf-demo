namespace SMF.Domain.Enums;

/// <summary>
/// Captured against a parental-consent request so the federation can
/// challenge anomalies (e.g. an unrelated adult signing for an athlete).
/// </summary>
public enum GuardianRelation
{
    Mother        = 0,
    Father        = 1,
    LegalGuardian = 2,
    Grandparent   = 3,
    Sibling       = 4,
    Other         = 99
}
