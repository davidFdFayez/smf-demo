namespace SMF.Infrastructure.Compliance;

/// <summary>
/// Configuration block for the compliance & governance module. Bound from
/// the <c>Compliance</c> section of <c>appsettings.json</c>.
/// </summary>
public sealed class ComplianceOptions
{
    public const string SectionName = "Compliance";

    /// <summary>HMAC secret used by <see cref="HmacConsentSigner"/>. Must
    /// be at least 32 bytes long in production. Defaults to an obvious
    /// placeholder so a misconfigured environment fails loudly rather than
    /// silently using zero-bytes.</summary>
    public string SigningSecret { get; set; } =
        "DEV-ONLY-CHANGE-ME-32-BYTES-MINIMUM-COMPLIANCE-SECRET";

    /// <summary>Public base URL the guardian-signing email links should
    /// point at (e.g. <c>https://federation.example.com</c>). Falls back to
    /// the request's host if left blank.</summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    public FileStorageOptions Storage { get; set; } = new();
}

public sealed class FileStorageOptions
{
    /// <summary>Selects the storage adapter at runtime. <c>Local</c> writes
    /// under <see cref="LocalRoot"/> on disk and serves files via the
    /// API's static-file middleware. <c>S3</c> would be added when a real
    /// bucket is wired up.</summary>
    public string Provider { get; set; } = "Local";

    /// <summary>Filesystem directory used by the local provider. Resolved
    /// relative to <c>ContentRootPath</c> if relative.</summary>
    public string LocalRoot { get; set; } = "App_Data/uploads";

    /// <summary>Public-facing URL prefix the API serves the local files
    /// from (matches the <c>UseStaticFiles</c> request path).</summary>
    public string PublicUrlPrefix { get; set; } = "/files";
}
