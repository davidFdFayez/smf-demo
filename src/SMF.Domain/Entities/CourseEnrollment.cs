using SMF.Domain.Enums;

namespace SMF.Domain.Entities;

/// <summary>
/// Tracks a member's progress through a <see cref="Course"/>. Owns the set
/// of completed lesson ids so progress surveys ("4 / 10 lessons") are a
/// single count, and completion (100%) triggers a certificate through the
/// application layer.
/// </summary>
public class CourseEnrollment
{
    public Guid Id { get; private set; }
    public Guid CourseId { get; private set; }
    public Guid MemberId { get; private set; }
    public DateTime EnrolledAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public CourseEnrollmentStatus Status { get; private set; }

    private readonly List<LessonCompletion> _completions = new();
    public IReadOnlyList<LessonCompletion> Completions => _completions.AsReadOnly();

    private CourseEnrollment() { }

    public static CourseEnrollment Enroll(Guid courseId, Guid memberId, DateTime nowUtc)
    {
        if (courseId == Guid.Empty) throw new ArgumentException("Course id required.", nameof(courseId));
        if (memberId == Guid.Empty) throw new ArgumentException("Member id required.", nameof(memberId));

        return new CourseEnrollment
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            MemberId = memberId,
            EnrolledAtUtc = nowUtc,
            Status = CourseEnrollmentStatus.Active
        };
    }

    public void MarkLessonComplete(Guid lessonId, DateTime nowUtc)
    {
        if (lessonId == Guid.Empty) throw new ArgumentException("Lesson id required.", nameof(lessonId));
        if (Status != CourseEnrollmentStatus.Active) return;
        if (_completions.Any(c => c.LessonId == lessonId)) return;
        _completions.Add(new LessonCompletion(Id, lessonId, nowUtc));
    }

    public void CompleteIfAllLessonsFinished(int totalLessons, DateTime nowUtc)
    {
        if (Status != CourseEnrollmentStatus.Active) return;
        if (totalLessons <= 0) return;
        if (_completions.Count >= totalLessons)
        {
            Status = CourseEnrollmentStatus.Completed;
            CompletedAtUtc = nowUtc;
        }
    }

    public void Drop() => Status = CourseEnrollmentStatus.Dropped;
}

public class LessonCompletion
{
    public Guid Id { get; private set; }
    public Guid EnrollmentId { get; private set; }
    public Guid LessonId { get; private set; }
    public DateTime CompletedAtUtc { get; private set; }

    private LessonCompletion() { }

    internal LessonCompletion(Guid enrollmentId, Guid lessonId, DateTime completedAtUtc)
    {
        Id = Guid.NewGuid();
        EnrollmentId = enrollmentId;
        LessonId = lessonId;
        CompletedAtUtc = completedAtUtc;
    }
}
