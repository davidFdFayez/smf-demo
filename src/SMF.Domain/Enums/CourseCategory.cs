namespace SMF.Domain.Enums;

/// <summary>
/// Course category for the federation e-learning platform. Mirrors PDF
/// §9 "Advanced Features → E-learning". Keeps the curriculum navigable by
/// target audience.
/// </summary>
public enum CourseCategory
{
    AthleteFundamentals = 1,
    CoachingCertification = 2,
    RefereeEducation = 3,
    SafeguardingAndIntegrity = 4,
    AntiDoping = 5,
    StrengthAndConditioning = 6,
    General = 7
}

public enum CourseLevel
{
    Beginner = 1,
    Intermediate = 2,
    Advanced = 3,
    Certification = 4
}

public enum CourseEnrollmentStatus
{
    Active = 1,
    Completed = 2,
    Dropped = 3
}
