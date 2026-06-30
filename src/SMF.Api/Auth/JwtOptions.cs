namespace SMF.Api.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "smf-api";
    public string Audience { get; set; } = "smf-clients";

    /// <summary>
    /// Symmetric signing key. In production this MUST be loaded from a secret
    /// store (e.g. Azure Key Vault). The dev value in appsettings is only for
    /// local development and tests.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    public int TokenLifetimeMinutes { get; set; } = 480;
}
