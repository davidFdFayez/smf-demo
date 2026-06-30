using SMF.Domain.Enums;

namespace SMF.Domain.Entities;

/// <summary>
/// A federation-registered club (PDF §1 "Club Directory" + §4 "Club
/// Administration"). Owns a set of affiliated members via
/// <see cref="Member.AffiliatedClubId"/> and can be granted its own branded
/// microsite once promoted to <see cref="ClubStatus.Active"/>.
/// </summary>
public class Club
{
    public Guid Id { get; private set; }

    /// <summary>Canonical display name, e.g. "Riyadh MuayThai Academy".</summary>
    public string Name { get; private set; } = default!;

    /// <summary>URL-safe identifier used in public directory links (e.g. "riyadh-muaythai").</summary>
    public string Slug { get; private set; } = default!;

    public string City { get; private set; } = default!;
    public string ContactEmail { get; private set; } = default!;
    public string ContactPhone { get; private set; } = default!;
    public string? WebsiteUrl { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? Description { get; private set; }

    public ClubStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    // Microsite fields (PDF §9 "Advanced Features → Club microsites").
    // All optional — a club that hasn't configured its microsite still shows
    // up in the directory with the core profile only.
    public string? MicrositeHeadline { get; private set; }
    public string? MicrositeAbout { get; private set; }
    public string? MicrositeHeroImageUrl { get; private set; }
    public string? MicrositePrimaryColor { get; private set; }
    public string? MicrositeInstagramHandle { get; private set; }
    public string? MicrositeTwitterHandle { get; private set; }
    public string? MicrositeYoutubeChannel { get; private set; }

    private Club() { }

    public static Club Apply(
        string name,
        string city,
        string contactEmail,
        string contactPhone,
        string? websiteUrl,
        string? description,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Club name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(city))
            throw new ArgumentException("City is required.", nameof(city));
        if (string.IsNullOrWhiteSpace(contactEmail))
            throw new ArgumentException("Contact email is required.", nameof(contactEmail));
        if (string.IsNullOrWhiteSpace(contactPhone))
            throw new ArgumentException("Contact phone is required.", nameof(contactPhone));

        return new Club
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Slug = BuildSlug(name),
            City = city.Trim(),
            ContactEmail = contactEmail.Trim(),
            ContactPhone = contactPhone.Trim(),
            WebsiteUrl = string.IsNullOrWhiteSpace(websiteUrl) ? null : websiteUrl.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Status = ClubStatus.Pending,
            CreatedAtUtc = nowUtc
        };
    }

    /// <summary>
    /// Appends a numeric suffix to the slug — used by the application layer
    /// when another club has already claimed the base slug.
    /// </summary>
    public void ApplySlugSuffix(int suffix)
    {
        if (suffix <= 0) throw new ArgumentOutOfRangeException(nameof(suffix));
        // Strip an existing numeric suffix (if any) before re-applying so
        // sequential collisions don't produce "my-club-1-2-3".
        var baseSlug = Slug;
        var dashIdx = baseSlug.LastIndexOf('-');
        if (dashIdx > 0 && int.TryParse(baseSlug[(dashIdx + 1)..], out _))
            baseSlug = baseSlug[..dashIdx];
        Slug = $"{baseSlug}-{suffix}";
    }

    public void Approve() => Status = ClubStatus.Active;
    public void Suspend() => Status = ClubStatus.Suspended;
    public void Reactivate() => Status = ClubStatus.Active;

    public void UpdateMicrosite(
        string? headline,
        string? about,
        string? heroImageUrl,
        string? primaryColor,
        string? instagramHandle,
        string? twitterHandle,
        string? youtubeChannel)
    {
        MicrositeHeadline = string.IsNullOrWhiteSpace(headline) ? null : headline.Trim();
        MicrositeAbout = string.IsNullOrWhiteSpace(about) ? null : about.Trim();
        MicrositeHeroImageUrl = string.IsNullOrWhiteSpace(heroImageUrl) ? null : heroImageUrl.Trim();
        MicrositePrimaryColor = string.IsNullOrWhiteSpace(primaryColor) ? null : NormaliseHexOrThrow(primaryColor);
        MicrositeInstagramHandle = string.IsNullOrWhiteSpace(instagramHandle) ? null : instagramHandle.Trim().TrimStart('@');
        MicrositeTwitterHandle = string.IsNullOrWhiteSpace(twitterHandle) ? null : twitterHandle.Trim().TrimStart('@');
        MicrositeYoutubeChannel = string.IsNullOrWhiteSpace(youtubeChannel) ? null : youtubeChannel.Trim();
    }

    private static string NormaliseHexOrThrow(string hex)
    {
        var s = hex.Trim();
        if (!s.StartsWith('#')) s = "#" + s;
        if (s.Length != 4 && s.Length != 7)
            throw new ArgumentException($"'{hex}' is not a valid CSS hex colour.", nameof(hex));
        foreach (var c in s[1..])
            if (!Uri.IsHexDigit(c))
                throw new ArgumentException($"'{hex}' contains non-hex characters.", nameof(hex));
        return s.ToLowerInvariant();
    }

    public void UpdateProfile(
        string city,
        string contactEmail,
        string contactPhone,
        string? websiteUrl,
        string? logoUrl,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(city))
            throw new ArgumentException("City is required.", nameof(city));
        if (string.IsNullOrWhiteSpace(contactEmail))
            throw new ArgumentException("Contact email is required.", nameof(contactEmail));
        if (string.IsNullOrWhiteSpace(contactPhone))
            throw new ArgumentException("Contact phone is required.", nameof(contactPhone));

        City = city.Trim();
        ContactEmail = contactEmail.Trim();
        ContactPhone = contactPhone.Trim();
        WebsiteUrl = string.IsNullOrWhiteSpace(websiteUrl) ? null : websiteUrl.Trim();
        LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    private static string BuildSlug(string name)
    {
        var lower = name.Trim().ToLowerInvariant();
        var buf = new System.Text.StringBuilder(lower.Length);
        var prevDash = false;

        foreach (var c in lower)
        {
            if (char.IsLetterOrDigit(c))
            {
                buf.Append(c);
                prevDash = false;
            }
            else if (!prevDash && buf.Length > 0)
            {
                buf.Append('-');
                prevDash = true;
            }
        }

        var slug = buf.ToString().TrimEnd('-');

        // If the name has no ASCII letters at all (e.g. pure Arabic), fall
        // back to a short stable identifier so the URL stays routable.
        if (slug.Length == 0)
            slug = "club-" + Guid.NewGuid().ToString("N")[..8];

        return slug;
    }
}
