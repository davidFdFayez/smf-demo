using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Entities;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Clubs;

// ─── DTOs ───────────────────────────────────────────────────────────────────

public sealed record ClubSummary(
    Guid Id,
    string Name,
    string Slug,
    string City,
    string ContactEmail,
    string ContactPhone,
    string? WebsiteUrl,
    string? LogoUrl,
    string? Description,
    ClubStatus Status,
    int MemberCount,
    DateTime CreatedAtUtc);

// ─── Register a new club application ────────────────────────────────────────

public sealed record RegisterClubCommand(
    string Name,
    string City,
    string ContactEmail,
    string ContactPhone,
    string? WebsiteUrl,
    string? Description) : IRequest<ClubSummary>;

public sealed class RegisterClubCommandValidator : AbstractValidator<RegisterClubCommand>
{
    public RegisterClubCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ContactEmail).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.ContactPhone).NotEmpty().MaximumLength(32);
        RuleFor(x => x.WebsiteUrl).MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.WebsiteUrl));
        RuleFor(x => x.Description).MaximumLength(2000)
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}

public sealed class RegisterClubCommandHandler : IRequestHandler<RegisterClubCommand, ClubSummary>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public RegisterClubCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<ClubSummary> Handle(RegisterClubCommand request, CancellationToken ct)
    {
        var club = Club.Apply(
            request.Name,
            request.City,
            request.ContactEmail,
            request.ContactPhone,
            request.WebsiteUrl,
            request.Description,
            _clock.UtcNow);

        // Slug uniqueness — bump with a suffix on collision rather than fail.
        var suffix = 1;
        while (await _db.Clubs.AnyAsync(c => c.Slug == club.Slug, ct))
        {
            club.ApplySlugSuffix(suffix++);
        }

        _db.Clubs.Add(club);
        await _db.SaveChangesAsync(ct);

        return ClubMapper.ToSummary(club, 0);
    }
}

// ─── Approve ────────────────────────────────────────────────────────────────

public sealed record ApproveClubCommand(Guid ClubId) : IRequest;

public sealed class ApproveClubCommandHandler : IRequestHandler<ApproveClubCommand>
{
    private readonly IApplicationDbContext _db;
    public ApproveClubCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(ApproveClubCommand request, CancellationToken ct)
    {
        var club = await _db.Clubs.FirstOrDefaultAsync(c => c.Id == request.ClubId, ct)
            ?? throw new NotFoundException("Club", request.ClubId);

        club.Approve();
        await _db.SaveChangesAsync(ct);
    }
}

// ─── List ───────────────────────────────────────────────────────────────────

public sealed record ListClubsQuery(ClubStatus? Status = null) : IRequest<IReadOnlyList<ClubSummary>>;

public sealed class ListClubsQueryHandler : IRequestHandler<ListClubsQuery, IReadOnlyList<ClubSummary>>
{
    private readonly IApplicationDbContext _db;
    public ListClubsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<ClubSummary>> Handle(ListClubsQuery request, CancellationToken ct)
    {
        var query = _db.Clubs.AsNoTracking();
        if (request.Status is { } s) query = query.Where(c => c.Status == s);

        var clubs = await query
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        // One roundtrip for all member counts keeps this page-load-ready even
        // at a few hundred clubs. If the directory grows to thousands, swap
        // for a projection-level groupby.
        var ids = clubs.Select(c => (Guid?)c.Id).ToList();
        var counts = await _db.Members
            .AsNoTracking()
            .Where(m => m.AffiliatedClubId != null && ids.Contains(m.AffiliatedClubId))
            .GroupBy(m => m.AffiliatedClubId!.Value)
            .Select(g => new { ClubId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var countMap = counts.ToDictionary(x => x.ClubId, x => x.Count);
        return clubs.Select(c => ClubMapper.ToSummary(c, countMap.GetValueOrDefault(c.Id))).ToList();
    }
}

// ─── Get by id / slug ──────────────────────────────────────────────────────

public sealed record GetClubBySlugQuery(string Slug) : IRequest<ClubSummary>;

public sealed class GetClubBySlugQueryHandler : IRequestHandler<GetClubBySlugQuery, ClubSummary>
{
    private readonly IApplicationDbContext _db;
    public GetClubBySlugQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<ClubSummary> Handle(GetClubBySlugQuery request, CancellationToken ct)
    {
        var club = await _db.Clubs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Slug == request.Slug, ct)
            ?? throw new NotFoundException("Club", request.Slug);

        var count = await _db.Members.AsNoTracking()
            .CountAsync(m => m.AffiliatedClubId == club.Id, ct);

        return ClubMapper.ToSummary(club, count);
    }
}

// ─── Mapper ─────────────────────────────────────────────────────────────────

internal static class ClubMapper
{
    public static ClubSummary ToSummary(Club c, int memberCount) => new(
        c.Id, c.Name, c.Slug, c.City, c.ContactEmail, c.ContactPhone,
        c.WebsiteUrl, c.LogoUrl, c.Description, c.Status, memberCount, c.CreatedAtUtc);
}
