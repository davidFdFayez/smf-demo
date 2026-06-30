namespace SMF.Domain.Enums;

/// <summary>
/// Categories of governance documents the federation publishes for SOPC
/// transparency requirements. Stored as a string in the database (not the
/// numeric value) so the audit trail stays readable if values are added or
/// renumbered later.
/// </summary>
public enum GovernanceDocumentType
{
    AnnualReport       = 0,
    AntiDopingPolicy   = 1,
    AthleteProtection  = 2,
    Statutes           = 3,
    CodeOfConduct      = 4,
    SafeguardingPolicy = 5,
    FinancialReport    = 6,
    Strategy           = 7,
    BoardMinutes       = 8,
    Other              = 99
}
