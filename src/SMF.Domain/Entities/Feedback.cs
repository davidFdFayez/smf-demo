using SMF.Domain.Enums;

namespace SMF.Domain.Entities;

/// <summary>
/// User-submitted rating + comment that can target the federation generally
/// (<see cref="FeedbackSubjectType.General"/>) or a specific event / product /
/// course. Submissions land in <see cref="FeedbackStatus.Pending"/> and an
/// admin promotes them to <see cref="FeedbackStatus.Public"/> before they
/// appear on the testimonial / showcase feed.
///
/// Both authenticated members and guests can submit; <see cref="AuthorMemberId"/>
/// is set for the former and the (Name, Email) pair is captured for both.
/// </summary>
public class Feedback
{
    public Guid Id { get; private set; }
    public FeedbackSubjectType SubjectType { get; private set; }

    /// <summary>Guid of the targeted entity (event/product/course). <c>null</c>
    /// for <see cref="FeedbackSubjectType.General"/>.</summary>
    public Guid? SubjectId { get; private set; }

    /// <summary>1–5 star rating (inclusive on both bounds).</summary>
    public int Rating { get; private set; }

    public string Comment { get; private set; } = default!;

    public Guid? AuthorMemberId { get; private set; }
    public string AuthorName { get; private set; } = default!;
    public string? AuthorEmail { get; private set; }

    public FeedbackStatus Status { get; private set; }
    public string? AdminNotes { get; private set; }
    public Guid? ModeratedByMemberId { get; private set; }
    public DateTime? ModeratedAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private Feedback() { }

    public static Feedback Submit(
        FeedbackSubjectType subjectType,
        Guid? subjectId,
        int rating,
        string comment,
        Guid? authorMemberId,
        string authorName,
        string? authorEmail,
        DateTime nowUtc)
    {
        if (subjectType != FeedbackSubjectType.General && (subjectId is null || subjectId == Guid.Empty))
            throw new ArgumentException(
                $"SubjectId is required for SubjectType '{subjectType}'.", nameof(subjectId));

        if (subjectType == FeedbackSubjectType.General && subjectId is not null && subjectId != Guid.Empty)
            // Don't pretend the FK is meaningful for general feedback.
            subjectId = null;

        if (rating < 1 || rating > 5)
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5.");

        if (string.IsNullOrWhiteSpace(comment))
            throw new ArgumentException("Comment required.", nameof(comment));
        if (string.IsNullOrWhiteSpace(authorName))
            throw new ArgumentException("Author name required.", nameof(authorName));

        return new Feedback
        {
            Id             = Guid.NewGuid(),
            SubjectType    = subjectType,
            SubjectId      = subjectId,
            Rating         = rating,
            Comment        = comment.Trim(),
            AuthorMemberId = authorMemberId == Guid.Empty ? null : authorMemberId,
            AuthorName     = authorName.Trim(),
            AuthorEmail    = string.IsNullOrWhiteSpace(authorEmail) ? null : authorEmail.Trim(),
            Status         = FeedbackStatus.Pending,
            CreatedAtUtc   = nowUtc
        };
    }

    public void Moderate(FeedbackStatus newStatus, Guid? moderatorMemberId, string? notes, DateTime nowUtc)
    {
        if (newStatus == FeedbackStatus.Pending)
            throw new ArgumentException("Cannot moderate back to Pending.", nameof(newStatus));

        Status              = newStatus;
        AdminNotes          = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        ModeratedByMemberId = moderatorMemberId == Guid.Empty ? null : moderatorMemberId;
        ModeratedAtUtc      = nowUtc;
    }
}
