using SMF.Domain.Enums;

namespace SMF.Domain.Entities;

/// <summary>
/// A federation news / announcement article. Surfaced on the public homepage
/// feed (PDF §1) and managed through the admin CMS-lite (PDF §4).
///
/// Lifecycle:
///   Draft  → Published (admin publishes)  → Archived (removed from public feed)
/// We keep the draft/published split explicit so content editors can prepare
/// articles ahead of time without leaking half-finished copy to the public.
/// </summary>
public class NewsArticle
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public string Summary { get; private set; } = default!;
    public string Body { get; private set; } = default!;
    public NewsCategory Category { get; private set; }
    public string? CoverImageUrl { get; private set; }
    public string AuthorDisplayName { get; private set; } = default!;
    public bool IsPublished { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private NewsArticle() { }

    private NewsArticle(
        Guid id,
        string title,
        string slug,
        string summary,
        string body,
        NewsCategory category,
        string? coverImageUrl,
        string authorDisplayName)
    {
        Id = id;
        Title = title;
        Slug = slug;
        Summary = summary;
        Body = body;
        Category = category;
        CoverImageUrl = coverImageUrl;
        AuthorDisplayName = authorDisplayName;
        IsPublished = false;
        IsArchived = false;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static NewsArticle Draft(
        string title,
        string summary,
        string body,
        NewsCategory category,
        string authorDisplayName,
        string? coverImageUrl = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(summary))
            throw new ArgumentException("Summary is required.", nameof(summary));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Body is required.", nameof(body));
        if (string.IsNullOrWhiteSpace(authorDisplayName))
            throw new ArgumentException("Author is required.", nameof(authorDisplayName));

        var slug = BuildSlug(title);

        return new NewsArticle(
            Guid.NewGuid(),
            title.Trim(),
            slug,
            summary.Trim(),
            body.Trim(),
            category,
            string.IsNullOrWhiteSpace(coverImageUrl) ? null : coverImageUrl.Trim(),
            authorDisplayName.Trim());
    }

    public void Publish()
    {
        if (IsArchived)
            throw new InvalidOperationException("Archived articles cannot be re-published.");
        if (IsPublished) return;

        IsPublished = true;
        PublishedAtUtc = DateTime.UtcNow;
    }

    public void Archive()
    {
        IsArchived = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateContent(
        string title,
        string summary,
        string body,
        NewsCategory category,
        string? coverImageUrl)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(summary))
            throw new ArgumentException("Summary is required.", nameof(summary));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Body is required.", nameof(body));

        Title = title.Trim();
        Summary = summary.Trim();
        Body = body.Trim();
        Category = category;
        CoverImageUrl = string.IsNullOrWhiteSpace(coverImageUrl) ? null : coverImageUrl.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Appends a numeric disambiguator to the slug (e.g. <c>"finals-recap-2"</c>)
    /// when another article has already claimed the base slug. Public so the
    /// Application layer can probe and retry — same pattern as
    /// <see cref="Club.ApplySlugSuffix"/>.
    /// </summary>
    public void ApplySlugSuffix(int suffix)
    {
        if (suffix < 1) throw new ArgumentOutOfRangeException(nameof(suffix));
        Slug = $"{BuildSlug(Title)}-{suffix}";
    }

    private static string BuildSlug(string title)
    {
        var lower = title.Trim().ToLowerInvariant();
        var chars = new System.Text.StringBuilder(lower.Length);
        var lastWasDash = false;
        foreach (var ch in lower)
        {
            if (char.IsLetterOrDigit(ch))
            {
                chars.Append(ch);
                lastWasDash = false;
            }
            else if (!lastWasDash)
            {
                chars.Append('-');
                lastWasDash = true;
            }
        }
        var slug = chars.ToString().Trim('-');
        return string.IsNullOrEmpty(slug) ? Guid.NewGuid().ToString("N")[..8] : slug;
    }
}
