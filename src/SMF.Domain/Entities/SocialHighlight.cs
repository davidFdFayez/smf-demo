using SMF.Domain.Enums;

namespace SMF.Domain.Entities;

/// <summary>
/// Admin-curated featured social media post displayed in the federation's
/// public "Latest from social" section. Storing curated posts (instead of
/// integrating against the Instagram Graph or X API directly) keeps the
/// public site online when those APIs change auth or rate-limit us — the
/// admin still pastes the post URL and optional embed snippet, and the
/// frontend renders the official platform embed client-side.
/// </summary>
public class SocialHighlight
{
    public Guid Id { get; private set; }
    public SocialPlatform Platform { get; private set; }
    public string Caption { get; private set; } = default!;

    /// <summary>Canonical URL of the post on its source platform.</summary>
    public string ExternalUrl { get; private set; } = default!;

    /// <summary>Optional pasted embed snippet (Instagram / X give a copy-paste
    /// blockquote + script). When present the frontend prefers this; otherwise
    /// it falls back to a plain card with image + link.</summary>
    public string? EmbedHtml { get; private set; }

    /// <summary>Optional preview image URL (used when no embed snippet is
    /// available, e.g. linking to a YouTube thumbnail or a hosted image).</summary>
    public string? MediaUrl { get; private set; }

    public int DisplayOrder { get; private set; }
    public bool IsPublished { get; private set; }
    public DateTime? PostedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private SocialHighlight() { }

    public static SocialHighlight Create(
        SocialPlatform platform,
        string caption,
        string externalUrl,
        string? embedHtml,
        string? mediaUrl,
        int displayOrder,
        bool isPublished,
        DateTime? postedAtUtc,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(caption))     throw new ArgumentException("Caption required.",     nameof(caption));
        if (string.IsNullOrWhiteSpace(externalUrl)) throw new ArgumentException("ExternalUrl required.", nameof(externalUrl));
        if (!Uri.TryCreate(externalUrl, UriKind.Absolute, out _))
            throw new ArgumentException("ExternalUrl must be an absolute URL.", nameof(externalUrl));

        return new SocialHighlight
        {
            Id           = Guid.NewGuid(),
            Platform     = platform,
            Caption      = caption.Trim(),
            ExternalUrl  = externalUrl.Trim(),
            EmbedHtml    = string.IsNullOrWhiteSpace(embedHtml) ? null : embedHtml,
            MediaUrl     = string.IsNullOrWhiteSpace(mediaUrl)  ? null : mediaUrl.Trim(),
            DisplayOrder = displayOrder,
            IsPublished  = isPublished,
            PostedAtUtc  = postedAtUtc,
            CreatedAtUtc = nowUtc
        };
    }

    public void Update(
        SocialPlatform platform,
        string caption,
        string externalUrl,
        string? embedHtml,
        string? mediaUrl,
        int displayOrder,
        bool isPublished,
        DateTime? postedAtUtc,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(caption))     throw new ArgumentException("Caption required.",     nameof(caption));
        if (string.IsNullOrWhiteSpace(externalUrl)) throw new ArgumentException("ExternalUrl required.", nameof(externalUrl));
        if (!Uri.TryCreate(externalUrl, UriKind.Absolute, out _))
            throw new ArgumentException("ExternalUrl must be an absolute URL.", nameof(externalUrl));

        Platform     = platform;
        Caption      = caption.Trim();
        ExternalUrl  = externalUrl.Trim();
        EmbedHtml    = string.IsNullOrWhiteSpace(embedHtml) ? null : embedHtml;
        MediaUrl     = string.IsNullOrWhiteSpace(mediaUrl)  ? null : mediaUrl.Trim();
        DisplayOrder = displayOrder;
        IsPublished  = isPublished;
        PostedAtUtc  = postedAtUtc;
        UpdatedAtUtc = nowUtc;
    }
}
