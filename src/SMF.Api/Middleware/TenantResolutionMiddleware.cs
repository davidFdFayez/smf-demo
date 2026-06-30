using MediatR;
using Microsoft.Extensions.Caching.Memory;
using SMF.Application.Common.Interfaces;
using SMF.Application.Features.Tenants;

namespace SMF.Api.Middleware;

/// <summary>
/// Resolves the white-label scoring tenant for the current request and
/// stamps it onto <see cref="ITenantContext"/> so downstream code
/// (rendering, branding, query filters) can pick it up without parsing
/// query strings or headers themselves.
///
/// Resolution order — first match wins:
/// <list type="number">
///   <item>The request's <c>Host</c> header matches a configured
///         <c>ScoringTenant.CustomDomain</c>. Highest trust — the host
///         can't be spoofed without DNS control.</item>
///   <item>An explicit <c>X-Tenant</c> header (server-to-server).</item>
///   <item>A <c>?tenant=CODE</c> query string (used by the public watch
///         page so demo links keep working).</item>
/// </list>
///
/// All lookups are cached for 60 seconds in-memory to keep the hot path
/// off the database; provisioning a new tenant takes effect within one
/// minute without process restart.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(
        RequestDelegate next,
        ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext http,
        ITenantContext tenant,
        ISender mediator,
        IMemoryCache cache)
    {
        try
        {
            var dto = await ResolveAsync(http, mediator, cache);
            if (dto is not null)
            {
                tenant.Set(
                    dto.Id, dto.Code, dto.DisplayName,
                    dto.PrimaryColor, dto.AccentColor,
                    dto.LogoUrl, dto.CustomDomain);
            }
        }
        catch (Exception ex)
        {
            // Tenant resolution must never break a request — single-tenant
            // callers see no tenant and continue with defaults.
            _logger.LogWarning(ex, "Tenant resolution failed; continuing without tenant context.");
        }

        await _next(http);
    }

    private static async Task<ScoringTenantDto?> ResolveAsync(
        HttpContext http, ISender mediator, IMemoryCache cache)
    {
        // 1. Host header → custom domain
        var host = http.Request.Host.Host?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(host) &&
            host != "localhost" && host != "127.0.0.1" && host != "::1")
        {
            var byHost = await cache.GetOrCreateAsync(
                $"tenant:host:{host}",
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = CacheTtl;
                    return await mediator.Send(new GetTenantByHostQuery(host));
                });
            if (byHost is not null) return byHost;
        }

        // 2. Explicit header (service-to-service)
        if (http.Request.Headers.TryGetValue("X-Tenant", out var headerValues))
        {
            var code = headerValues.ToString().Trim();
            if (!string.IsNullOrEmpty(code))
            {
                var byHeader = await TryByCode(code, mediator, cache);
                if (byHeader is not null) return byHeader;
            }
        }

        // 3. Query string fallback (kept for the existing /watch?tenant=CODE flow)
        if (http.Request.Query.TryGetValue("tenant", out var qsValues))
        {
            var code = qsValues.ToString().Trim();
            if (!string.IsNullOrEmpty(code))
            {
                var byQuery = await TryByCode(code, mediator, cache);
                if (byQuery is not null) return byQuery;
            }
        }

        return null;
    }

    private static async Task<ScoringTenantDto?> TryByCode(
        string code, ISender mediator, IMemoryCache cache)
    {
        return await cache.GetOrCreateAsync(
            $"tenant:code:{code.ToLowerInvariant()}",
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheTtl;
                try
                {
                    return await mediator.Send(new GetTenantByCodeQuery(code));
                }
                catch
                {
                    // GetTenantByCode throws NotFound when the code is
                    // bogus or inactive — treat that as "no tenant" so
                    // callers fall through to the next resolution step.
                    return null;
                }
            });
    }
}
