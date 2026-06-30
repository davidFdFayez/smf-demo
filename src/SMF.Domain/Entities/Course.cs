using SMF.Domain.Enums;

namespace SMF.Domain.Entities;

/// <summary>
/// Federation e-learning course (PDF §9 "Advanced Features → E-learning").
/// Aggregate root: owns a list of ordered <see cref="Lesson"/>s. Publishable
/// lifecycle mirrors news articles so content editors can author drafts
/// without leaking half-finished curricula to the public.
/// </summary>
public class Course
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public string Summary { get; private set; } = default!;
    public CourseCategory Category { get; private set; }
    public CourseLevel Level { get; private set; }
    public string InstructorName { get; private set; } = default!;
    public string? CoverImageUrl { get; private set; }
    public bool IsPublished { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }

    private readonly List<Lesson> _lessons = new();
    public IReadOnlyList<Lesson> Lessons => _lessons.AsReadOnly();

    private Course() { }

    public static Course Draft(
        string title,
        string summary,
        CourseCategory category,
        CourseLevel level,
        string instructorName,
        string? coverImageUrl,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title required.", nameof(title));
        if (string.IsNullOrWhiteSpace(summary)) throw new ArgumentException("Summary required.", nameof(summary));
        if (string.IsNullOrWhiteSpace(instructorName))
            throw new ArgumentException("Instructor name required.", nameof(instructorName));

        return new Course
        {
            Id = Guid.NewGuid(),
            Title = title.Trim(),
            Slug = BuildSlug(title),
            Summary = summary.Trim(),
            Category = category,
            Level = level,
            InstructorName = instructorName.Trim(),
            CoverImageUrl = string.IsNullOrWhiteSpace(coverImageUrl) ? null : coverImageUrl.Trim(),
            IsPublished = false,
            IsArchived = false,
            CreatedAtUtc = nowUtc
        };
    }

    public void Publish()
    {
        if (IsArchived) throw new InvalidOperationException("Archived courses cannot be published.");
        if (_lessons.Count == 0)
            throw new InvalidOperationException("Course must have at least one lesson before publishing.");
        if (IsPublished) return;
        IsPublished = true;
        PublishedAtUtc = DateTime.UtcNow;
    }

    public void Archive()
    {
        IsArchived = true;
        IsPublished = false;
    }

    public void UpdateContent(
        string title,
        string summary,
        CourseCategory category,
        CourseLevel level,
        string instructorName,
        string? coverImageUrl)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title required.", nameof(title));
        if (string.IsNullOrWhiteSpace(summary)) throw new ArgumentException("Summary required.", nameof(summary));
        if (string.IsNullOrWhiteSpace(instructorName))
            throw new ArgumentException("Instructor name required.", nameof(instructorName));

        Title = title.Trim();
        Summary = summary.Trim();
        Category = category;
        Level = level;
        InstructorName = instructorName.Trim();
        CoverImageUrl = string.IsNullOrWhiteSpace(coverImageUrl) ? null : coverImageUrl.Trim();
    }

    public Lesson AddLesson(
        string title,
        string summary,
        string content,
        string? videoUrl,
        int estimatedMinutes,
        DateTime nowUtc)
    {
        var order = _lessons.Count == 0 ? 0 : _lessons.Max(l => l.Order) + 1;
        var lesson = Lesson.Create(Id, order, title, summary, content, videoUrl, estimatedMinutes, nowUtc);
        _lessons.Add(lesson);
        return lesson;
    }

    public void RemoveLesson(Guid lessonId)
    {
        var lesson = _lessons.FirstOrDefault(l => l.Id == lessonId)
                     ?? throw new InvalidOperationException($"Lesson {lessonId} not found in course {Id}.");
        _lessons.Remove(lesson);
        // Re-order remaining lessons so UI doesn't show gaps.
        var ordered = _lessons.OrderBy(l => l.Order).ToList();
        for (var i = 0; i < ordered.Count; i++) ordered[i].Reorder(i);
    }

    public void ApplySlugSuffix(int suffix)
    {
        if (suffix < 1) throw new ArgumentOutOfRangeException(nameof(suffix));
        Slug = $"{BuildSlug(Title)}-{suffix}";
    }

    private static string BuildSlug(string title)
    {
        var lower = title.Trim().ToLowerInvariant();
        var buf = new System.Text.StringBuilder(lower.Length);
        var prev = false;
        foreach (var c in lower)
        {
            if (char.IsLetterOrDigit(c)) { buf.Append(c); prev = false; }
            else if (!prev && buf.Length > 0) { buf.Append('-'); prev = true; }
        }
        var slug = buf.ToString().TrimEnd('-');
        return slug.Length == 0 ? "course-" + Guid.NewGuid().ToString("N")[..8] : slug;
    }
}
