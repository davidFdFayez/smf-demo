namespace SMF.Domain.Enums;

/// <summary>
/// Moderation states for <see cref="Entities.Feedback"/>. Submissions land as
/// <see cref="Pending"/>; admins promote to <see cref="Public"/> (visible on
/// the showcase feed) or <see cref="Hidden"/> (kept for audit, not displayed).
/// </summary>
public enum FeedbackStatus
{
    Pending = 0,
    Public  = 1,
    Hidden  = 2
}
