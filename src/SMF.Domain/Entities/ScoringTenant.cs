namespace SMF.Domain.Entities;

/// <summary>
/// A white-labelled scoring client (PDF §9 "Advanced Features →
/// White-label multi-tenant scoring"). Regional federations, licensed
/// promoters, or university leagues can point their scoreboards at our
/// hub and get their own brand, logo, and accent color applied on the
/// public <c>/watch?tenant=CODE</c> view.
///
/// The tenant's <see cref="Code"/> is the only thing we trust from the
/// query string — everything else (colors, logo) is resolved from the
/// server row so compromised query parameters can't mis-brand the feed.
/// </summary>
public class ScoringTenant
{
    public Guid Id { get; private set; }

    /// <summary>URL-safe identifier used in <c>?tenant=CODE</c>. Stable business key.</summary>
    public string Code { get; private set; } = default!;

    public string DisplayName { get; private set; } = default!;

    /// <summary>Primary brand colour (hex, e.g. <c>#0c6b3a</c>). Used for score chrome.</summary>
    public string PrimaryColor { get; private set; } = default!;

    /// <summary>Accent colour used on the score pill + primary fighter card.</summary>
    public string AccentColor { get; private set; } = default!;

    public string? LogoUrl { get; private set; }

    /// <summary>Federation-facing contact for billing / SLA issues.</summary>
    public string ContactEmail { get; private set; } = default!;

    /// <summary>
    /// Optional vanity host (e.g. <c>scoring.kuwaitmuaythai.org</c>). When the
    /// API receives a request whose Host header matches this value, the
    /// <c>TenantResolutionMiddleware</c> resolves the active tenant without
    /// requiring an explicit <c>?tenant=</c> query string. Stored lower-case,
    /// host only — no scheme, no port, no trailing slash.
    /// </summary>
    public string? CustomDomain { get; private set; }

    /// <summary>Optional public website for the tenant (linked from the watch UI footer).</summary>
    public string? WebsiteUrl { get; private set; }

    /// <summary>Optional dark-theme variant of the logo for the OBS overlay.</summary>
    public string? DarkLogoUrl { get; private set; }

    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private ScoringTenant() { }

    public static ScoringTenant Create(
        string code,
        string displayName,
        string primaryColor,
        string accentColor,
        string? logoUrl,
        string contactEmail,
        DateTime nowUtc,
        string? customDomain = null,
        string? websiteUrl = null,
        string? darkLogoUrl = null)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Tenant code required.", nameof(code));
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Display name required.", nameof(displayName));
        if (string.IsNullOrWhiteSpace(contactEmail)) throw new ArgumentException("Contact email required.", nameof(contactEmail));

        return new ScoringTenant
        {
            Id = Guid.NewGuid(),
            Code = NormaliseCode(code),
            DisplayName = displayName.Trim(),
            PrimaryColor = NormaliseHex(primaryColor),
            AccentColor = NormaliseHex(accentColor),
            LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim(),
            DarkLogoUrl = string.IsNullOrWhiteSpace(darkLogoUrl) ? null : darkLogoUrl.Trim(),
            ContactEmail = contactEmail.Trim(),
            CustomDomain = NormaliseHost(customDomain),
            WebsiteUrl = string.IsNullOrWhiteSpace(websiteUrl) ? null : websiteUrl.Trim(),
            IsActive = true,
            CreatedAtUtc = nowUtc
        };
    }

    public void UpdateBrand(
        string displayName,
        string primaryColor,
        string accentColor,
        string? logoUrl,
        string contactEmail,
        string? customDomain = null,
        string? websiteUrl = null,
        string? darkLogoUrl = null)
    {
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Display name required.", nameof(displayName));
        if (string.IsNullOrWhiteSpace(contactEmail)) throw new ArgumentException("Contact email required.", nameof(contactEmail));

        DisplayName = displayName.Trim();
        PrimaryColor = NormaliseHex(primaryColor);
        AccentColor = NormaliseHex(accentColor);
        LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim();
        DarkLogoUrl = string.IsNullOrWhiteSpace(darkLogoUrl) ? null : darkLogoUrl.Trim();
        ContactEmail = contactEmail.Trim();
        CustomDomain = NormaliseHost(customDomain);
        WebsiteUrl = string.IsNullOrWhiteSpace(websiteUrl) ? null : websiteUrl.Trim();
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;

    private static string NormaliseCode(string code)
    {
        var s = code.Trim().ToLowerInvariant();
        var buf = new System.Text.StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (char.IsLetterOrDigit(c) || c == '-') buf.Append(c);
        }
        var result = buf.ToString().Trim('-');
        if (result.Length < 2)
            throw new ArgumentException("Code must contain at least 2 alphanumerics.", nameof(code));
        return result;
    }

    /// <summary>
    /// Strip scheme/port/path and lower-case so the value compares cleanly
    /// against <c>HttpRequest.Host.Host</c>. Returns null when blank.
    /// </summary>
    private static string? NormaliseHost(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim().ToLowerInvariant();
        s = s.Replace("https://", string.Empty).Replace("http://", string.Empty);
        var slash = s.IndexOf('/');
        if (slash >= 0) s = s[..slash];
        var colon = s.IndexOf(':');
        if (colon >= 0) s = s[..colon];
        s = s.TrimEnd('.');
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    private static string NormaliseHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            throw new ArgumentException("Colour required.", nameof(hex));
        var s = hex.Trim();
        if (!s.StartsWith('#')) s = "#" + s;
        if (s.Length != 4 && s.Length != 7)
            throw new ArgumentException($"'{hex}' is not a valid CSS hex colour.", nameof(hex));
        foreach (var c in s[1..])
            if (!Uri.IsHexDigit(c))
                throw new ArgumentException($"'{hex}' contains non-hex characters.", nameof(hex));
        return s.ToLowerInvariant();
    }
}
