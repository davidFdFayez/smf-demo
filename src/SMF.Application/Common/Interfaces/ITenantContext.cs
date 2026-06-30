namespace SMF.Application.Common.Interfaces;

/// <summary>
/// Per-request tenant context. Populated by the API
/// <c>TenantResolutionMiddleware</c> (host header → custom domain →
/// query string fallback) and consumed by handlers, query filters,
/// and rendering code that needs to react to the active white-label
/// federation.
///
/// In single-tenant deployments <see cref="IsResolved"/> returns
/// <c>false</c> and every property is null — callers should treat
/// that as "use defaults".
/// </summary>
public interface ITenantContext
{
    bool IsResolved { get; }

    Guid? TenantId { get; }
    string? Code { get; }
    string? DisplayName { get; }
    string? PrimaryColor { get; }
    string? AccentColor { get; }
    string? LogoUrl { get; }
    string? CustomDomain { get; }

    /// <summary>
    /// Set once per request from the resolution middleware. Subsequent
    /// calls within the same request are ignored (the middleware uses
    /// the first resolved match — host wins over header which wins over
    /// query string).
    /// </summary>
    void Set(
        Guid tenantId,
        string code,
        string displayName,
        string primaryColor,
        string accentColor,
        string? logoUrl,
        string? customDomain);
}
