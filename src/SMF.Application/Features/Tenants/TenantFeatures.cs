using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Entities;

namespace SMF.Application.Features.Tenants;

// PDF §9 "Advanced Features → White-label multi-tenant scoring"
// Regional federations, promoters, and university leagues can be provisioned
// as tenants with their own colors + logo applied to the public
// /watch?tenant=CODE view.

public sealed record ScoringTenantDto(
    Guid Id,
    string Code,
    string DisplayName,
    string PrimaryColor,
    string AccentColor,
    string? LogoUrl,
    string? DarkLogoUrl,
    string ContactEmail,
    string? CustomDomain,
    string? WebsiteUrl,
    bool IsActive,
    DateTime CreatedAtUtc);

public sealed record ProvisionTenantCommand(
    string Code,
    string DisplayName,
    string PrimaryColor,
    string AccentColor,
    string? LogoUrl,
    string ContactEmail,
    string? CustomDomain = null,
    string? WebsiteUrl = null,
    string? DarkLogoUrl = null) : IRequest<ScoringTenantDto>;

public sealed class ProvisionTenantCommandValidator : AbstractValidator<ProvisionTenantCommand>
{
    public ProvisionTenantCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MinimumLength(2).MaximumLength(48);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.PrimaryColor).NotEmpty().MaximumLength(12);
        RuleFor(x => x.AccentColor).NotEmpty().MaximumLength(12);
        RuleFor(x => x.ContactEmail).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.LogoUrl).MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.LogoUrl));
        RuleFor(x => x.DarkLogoUrl).MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.DarkLogoUrl));
        RuleFor(x => x.CustomDomain).MaximumLength(253)
            .When(x => !string.IsNullOrWhiteSpace(x.CustomDomain));
        RuleFor(x => x.WebsiteUrl).MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.WebsiteUrl));
    }
}

public sealed class ProvisionTenantCommandHandler
    : IRequestHandler<ProvisionTenantCommand, ScoringTenantDto>
{
    private readonly IApplicationDbContext _db;
    public ProvisionTenantCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ScoringTenantDto> Handle(ProvisionTenantCommand request, CancellationToken ct)
    {
        var tenant = ScoringTenant.Create(
            request.Code, request.DisplayName,
            request.PrimaryColor, request.AccentColor,
            request.LogoUrl, request.ContactEmail, DateTime.UtcNow,
            request.CustomDomain, request.WebsiteUrl, request.DarkLogoUrl);

        if (await _db.ScoringTenants.AnyAsync(t => t.Code == tenant.Code, ct))
            throw new InvalidOperationException($"Tenant code '{tenant.Code}' is already in use.");

        if (!string.IsNullOrEmpty(tenant.CustomDomain) &&
            await _db.ScoringTenants.AnyAsync(t => t.CustomDomain == tenant.CustomDomain, ct))
        {
            throw new InvalidOperationException(
                $"Custom domain '{tenant.CustomDomain}' is already bound to another tenant.");
        }

        _db.ScoringTenants.Add(tenant);
        await _db.SaveChangesAsync(ct);
        return TenantMapper.ToDto(tenant);
    }
}

public sealed record UpdateTenantBrandCommand(
    Guid Id,
    string DisplayName,
    string PrimaryColor,
    string AccentColor,
    string? LogoUrl,
    string ContactEmail,
    string? CustomDomain = null,
    string? WebsiteUrl = null,
    string? DarkLogoUrl = null) : IRequest<ScoringTenantDto>;

public sealed class UpdateTenantBrandCommandHandler
    : IRequestHandler<UpdateTenantBrandCommand, ScoringTenantDto>
{
    private readonly IApplicationDbContext _db;
    public UpdateTenantBrandCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ScoringTenantDto> Handle(UpdateTenantBrandCommand request, CancellationToken ct)
    {
        var tenant = await _db.ScoringTenants.FirstOrDefaultAsync(t => t.Id == request.Id, ct)
                     ?? throw new NotFoundException(nameof(ScoringTenant), request.Id);
        tenant.UpdateBrand(
            request.DisplayName, request.PrimaryColor, request.AccentColor,
            request.LogoUrl, request.ContactEmail,
            request.CustomDomain, request.WebsiteUrl, request.DarkLogoUrl);

        if (!string.IsNullOrEmpty(tenant.CustomDomain) &&
            await _db.ScoringTenants.AnyAsync(
                t => t.CustomDomain == tenant.CustomDomain && t.Id != tenant.Id, ct))
        {
            throw new InvalidOperationException(
                $"Custom domain '{tenant.CustomDomain}' is already bound to another tenant.");
        }
        await _db.SaveChangesAsync(ct);
        return TenantMapper.ToDto(tenant);
    }
}

public sealed record SetTenantActiveCommand(Guid Id, bool IsActive) : IRequest<Unit>;

public sealed class SetTenantActiveCommandHandler : IRequestHandler<SetTenantActiveCommand, Unit>
{
    private readonly IApplicationDbContext _db;
    public SetTenantActiveCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Unit> Handle(SetTenantActiveCommand request, CancellationToken ct)
    {
        var tenant = await _db.ScoringTenants.FirstOrDefaultAsync(t => t.Id == request.Id, ct)
                     ?? throw new NotFoundException(nameof(ScoringTenant), request.Id);
        if (request.IsActive) tenant.Activate(); else tenant.Deactivate();
        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed record ListTenantsQuery : IRequest<IReadOnlyList<ScoringTenantDto>>;

public sealed class ListTenantsQueryHandler : IRequestHandler<ListTenantsQuery, IReadOnlyList<ScoringTenantDto>>
{
    private readonly IApplicationDbContext _db;
    public ListTenantsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<ScoringTenantDto>> Handle(ListTenantsQuery request, CancellationToken ct)
        => await _db.ScoringTenants.AsNoTracking()
            .OrderBy(t => t.DisplayName)
            .Select(t => new ScoringTenantDto(
                t.Id, t.Code, t.DisplayName, t.PrimaryColor, t.AccentColor,
                t.LogoUrl, t.DarkLogoUrl, t.ContactEmail,
                t.CustomDomain, t.WebsiteUrl, t.IsActive, t.CreatedAtUtc))
            .ToListAsync(ct);
}

/// <summary>
/// Lookup used by the API tenant-resolution middleware so a single SQL hit
/// can answer "what tenant owns this incoming Host header?".
/// </summary>
public sealed record GetTenantByHostQuery(string Host) : IRequest<ScoringTenantDto?>;

public sealed class GetTenantByHostQueryHandler
    : IRequestHandler<GetTenantByHostQuery, ScoringTenantDto?>
{
    private readonly IApplicationDbContext _db;
    public GetTenantByHostQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<ScoringTenantDto?> Handle(GetTenantByHostQuery request, CancellationToken ct)
    {
        var host = (request.Host ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(host)) return null;
        var t = await _db.ScoringTenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.IsActive && t.CustomDomain == host, ct);
        return t is null ? null : TenantMapper.ToDto(t);
    }
}

public sealed record GetTenantByCodeQuery(string Code) : IRequest<ScoringTenantDto>;

public sealed class GetTenantByCodeQueryHandler : IRequestHandler<GetTenantByCodeQuery, ScoringTenantDto>
{
    private readonly IApplicationDbContext _db;
    public GetTenantByCodeQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<ScoringTenantDto> Handle(GetTenantByCodeQuery request, CancellationToken ct)
    {
        var code = (request.Code ?? "").Trim().ToLowerInvariant();
        var tenant = await _db.ScoringTenants.AsNoTracking()
                         .FirstOrDefaultAsync(t => t.Code == code && t.IsActive, ct)
                     ?? throw new NotFoundException(nameof(ScoringTenant), code);
        return TenantMapper.ToDto(tenant);
    }
}

internal static class TenantMapper
{
    public static ScoringTenantDto ToDto(ScoringTenant t) => new(
        t.Id, t.Code, t.DisplayName, t.PrimaryColor, t.AccentColor,
        t.LogoUrl, t.DarkLogoUrl, t.ContactEmail,
        t.CustomDomain, t.WebsiteUrl, t.IsActive, t.CreatedAtUtc);
}
