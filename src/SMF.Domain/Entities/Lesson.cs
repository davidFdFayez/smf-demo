namespace SMF.Domain.Entities;

/// <summary>
/// A single ordered lesson inside a <see cref="Course"/>. Owned child —
/// lessons don't exist outside a course and are re-ordered / added / removed
/// through the parent aggregate to keep ordering invariants local.
/// </summary>
public class Lesson
{
    public Guid Id { get; private set; }
    public Guid CourseId { get; private set; }
    public int Order { get; private set; }
    public string Title { get; private set; } = default!;
    public string Summary { get; private set; } = default!;

    /// <summary>Markdown/HTML lesson body rendered on the reader page.</summary>
    public string Content { get; private set; } = default!;

    /// <summary>Optional external video URL (YouTube/Vimeo). Null for text-only lessons.</summary>
    public string? VideoUrl { get; private set; }

    /// <summary>Estimated completion time in minutes — surfaced on the catalog card.</summary>
    public int EstimatedMinutes { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private Lesson() { }

    internal static Lesson Create(
        Guid courseId,
        int order,
        string title,
        string summary,
        string content,
        string? videoUrl,
        int estimatedMinutes,
        DateTime nowUtc)
    {
        if (courseId == Guid.Empty) throw new ArgumentException("Course id required.", nameof(courseId));
        if (order < 0) throw new ArgumentOutOfRangeException(nameof(order));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title required.", nameof(title));
        if (string.IsNullOrWhiteSpace(summary)) throw new ArgumentException("Summary required.", nameof(summary));
        if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("Content required.", nameof(content));
        if (estimatedMinutes < 0) throw new ArgumentOutOfRangeException(nameof(estimatedMinutes));

        return new Lesson
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            Order = order,
            Title = title.Trim(),
            Summary = summary.Trim(),
            Content = content.Trim(),
            VideoUrl = string.IsNullOrWhiteSpace(videoUrl) ? null : videoUrl.Trim(),
            EstimatedMinutes = estimatedMinutes,
            CreatedAtUtc = nowUtc
        };
    }

    internal void Reorder(int newOrder)
    {
        if (newOrder < 0) throw new ArgumentOutOfRangeException(nameof(newOrder));
        Order = newOrder;
    }

    internal void Update(string title, string summary, string content, string? videoUrl, int estimatedMinutes)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title required.", nameof(title));
        if (string.IsNullOrWhiteSpace(summary)) throw new ArgumentException("Summary required.", nameof(summary));
        if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("Content required.", nameof(content));
        if (estimatedMinutes < 0) throw new ArgumentOutOfRangeException(nameof(estimatedMinutes));

        Title = title.Trim();
        Summary = summary.Trim();
        Content = content.Trim();
        VideoUrl = string.IsNullOrWhiteSpace(videoUrl) ? null : videoUrl.Trim();
        EstimatedMinutes = estimatedMinutes;
    }
}
