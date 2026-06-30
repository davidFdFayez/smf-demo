namespace SMF.Domain.Enums;

/// <summary>
/// Polymorphic anchor for a piece of <see cref="Entities.Feedback"/>. We persist
/// the type + a nullable <c>SubjectId</c> instead of separate FK columns so the
/// table stays flat and a single <c>WHERE</c> can pivot the public feed by domain.
/// </summary>
public enum FeedbackSubjectType
{
    /// <summary>Federation-wide feedback. <c>SubjectId</c> is null.</summary>
    General = 0,
    /// <summary>Feedback about a specific <see cref="Entities.FederationEvent"/>.</summary>
    Event   = 1,
    /// <summary>Review of a store <see cref="Entities.Product"/>.</summary>
    Product = 2,
    /// <summary>Course feedback (<see cref="Entities.Course"/>).</summary>
    Course  = 3
}
