namespace SMF.Domain.Enums;

/// <summary>
/// Categorises federation news so the homepage / public feed can be filtered
/// (PDF §1 "Latest federation announcements" + §4 "Content Management").
/// </summary>
public enum NewsCategory
{
    Announcement = 1,
    Championship = 2,
    Education = 3,
    PressRelease = 4,
    General = 5,
}
