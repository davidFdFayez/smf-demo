namespace SMF.Domain.Enums;

public enum RegistrationStatus
{
    /// <summary>Freshly registered; awaiting admin review.</summary>
    Pending = 0,

    /// <summary>Admin-approved; membership fee not yet paid.</summary>
    Approved = 1,

    /// <summary>Fully paid-up member with access to SMF services.</summary>
    Active = 2
}
