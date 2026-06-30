using SMF.Application.Common.Interfaces;

namespace SMF.Infrastructure.MultiTenancy;

/// <summary>
/// Default <see cref="ITenantContext"/>. Registered as scoped so each HTTP
/// request gets a fresh instance — the API tenant-resolution middleware
/// populates it before any handler runs.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    public bool IsResolved { get; private set; }

    public Guid? TenantId { get; private set; }
    public string? Code { get; private set; }
    public string? DisplayName { get; private set; }
    public string? PrimaryColor { get; private set; }
    public string? AccentColor { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? CustomDomain { get; private set; }

    public void Set(
        Guid tenantId,
        string code,
        string displayName,
        string primaryColor,
        string accentColor,
        string? logoUrl,
        string? customDomain)
    {
        if (IsResolved) return;
        TenantId      = tenantId;
        Code          = code;
        DisplayName   = displayName;
        PrimaryColor  = primaryColor;
        AccentColor   = accentColor;
        LogoUrl       = logoUrl;
        CustomDomain  = customDomain;
        IsResolved    = true;
    }
}
