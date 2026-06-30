using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Entities;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Clubs;

// PDF §9 "Advanced Features → Club microsites"
// A public profile page per club at /clubs/{slug} with branded hero, social
// links, roster preview and upcoming events they're connected to.

public sealed record ClubMicrositeRosterMember(
    Guid Id, string FullName, string SmfId, MemberRole Role);

public sealed record ClubMicrositeUpcomingEvent(
    Guid Id, string Title, string Location, DateTime StartsAtUtc);

public sealed record ClubMicrositeDetails(
    Guid Id,
    string Slug,
    string Name,
    string City,
    ClubStatus Status,
    string ContactEmail,
    string ContactPhone,
    string? WebsiteUrl,
    string? LogoUrl,
    string? Description,
    // Microsite-specific fields
    string? Headline,
    string? About,
    string? HeroImageUrl,
    string? PrimaryColor,
    string? InstagramHandle,
    string? TwitterHandle,
    string? YoutubeChannel,
    int MemberCount,
    IReadOnlyList<ClubMicrositeRosterMember> Roster,
    IReadOnlyList<ClubMicrositeUpcomingEvent> UpcomingEvents);

public sealed record GetClubMicrositeQuery(string Slug) : IRequest<ClubMicrositeDetails>;

public sealed class GetClubMicrositeQueryHandler
    : IRequestHandler<GetClubMicrositeQuery, ClubMicrositeDetails>
{
    private readonly IApplicationDbContext _db;
    public GetClubMicrositeQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<ClubMicrositeDetails> Handle(GetClubMicrositeQuery request, CancellationToken ct)
    {
        var club = await _db.Clubs.AsNoTracking()
                       .FirstOrDefaultAsync(c => c.Slug == request.Slug, ct)
                   ?? throw new NotFoundException(nameof(Club), request.Slug);

        // Only expose active clubs via the public microsite route — pending
        // / suspended clubs still live in the admin directory but should
        // not appear on the public site. Return 404-equivalent for those.
        if (club.Status != ClubStatus.Active)
            throw new NotFoundException(nameof(Club), request.Slug);

        var roster = await _db.Members.AsNoTracking()
            .Where(m => m.AffiliatedClubId == club.Id
                        && m.RegistrationStatus == RegistrationStatus.Approved)
            .OrderBy(m => m.FullName)
            .Take(12)
            .Select(m => new ClubMicrositeRosterMember(m.Id, m.FullName, m.SMF_ID, m.Role))
            .ToListAsync(ct);

        var memberCount = await _db.Members
            .CountAsync(m => m.AffiliatedClubId == club.Id, ct);

        var now = DateTime.UtcNow;
        // Upcoming federation events for context — all clubs see the shared
        // federation calendar until we model per-club events.
        var events = await _db.Events.AsNoTracking()
            .Where(e => e.StartsAtUtc >= now
                        && e.Status != EventStatus.Cancelled
                        && e.Status != EventStatus.Draft)
            .OrderBy(e => e.StartsAtUtc)
            .Take(3)
            .Select(e => new ClubMicrositeUpcomingEvent(e.Id, e.Title, e.Location, e.StartsAtUtc))
            .ToListAsync(ct);

        return new ClubMicrositeDetails(
            club.Id, club.Slug, club.Name, club.City, club.Status,
            club.ContactEmail, club.ContactPhone, club.WebsiteUrl, club.LogoUrl, club.Description,
            club.MicrositeHeadline, club.MicrositeAbout, club.MicrositeHeroImageUrl,
            club.MicrositePrimaryColor, club.MicrositeInstagramHandle,
            club.MicrositeTwitterHandle, club.MicrositeYoutubeChannel,
            memberCount, roster, events);
    }
}

public sealed record UpdateClubMicrositeCommand(
    Guid ClubId,
    string? Headline,
    string? About,
    string? HeroImageUrl,
    string? PrimaryColor,
    string? InstagramHandle,
    string? TwitterHandle,
    string? YoutubeChannel) : IRequest<ClubMicrositeDetails>;

public sealed class UpdateClubMicrositeCommandValidator : AbstractValidator<UpdateClubMicrositeCommand>
{
    public UpdateClubMicrositeCommandValidator()
    {
        RuleFor(x => x.ClubId).NotEmpty();
        RuleFor(x => x.Headline).MaximumLength(200);
        RuleFor(x => x.About).MaximumLength(4000);
        RuleFor(x => x.HeroImageUrl).MaximumLength(500);
        RuleFor(x => x.PrimaryColor).MaximumLength(12);
        RuleFor(x => x.InstagramHandle).MaximumLength(64);
        RuleFor(x => x.TwitterHandle).MaximumLength(64);
        RuleFor(x => x.YoutubeChannel).MaximumLength(120);
    }
}

public sealed class UpdateClubMicrositeCommandHandler
    : IRequestHandler<UpdateClubMicrositeCommand, ClubMicrositeDetails>
{
    private readonly IApplicationDbContext _db;

    public UpdateClubMicrositeCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ClubMicrositeDetails> Handle(
        UpdateClubMicrositeCommand request, CancellationToken ct)
    {
        var club = await _db.Clubs.FirstOrDefaultAsync(c => c.Id == request.ClubId, ct)
                   ?? throw new NotFoundException(nameof(Club), request.ClubId);

        club.UpdateMicrosite(
            request.Headline, request.About, request.HeroImageUrl,
            request.PrimaryColor, request.InstagramHandle,
            request.TwitterHandle, request.YoutubeChannel);

        await _db.SaveChangesAsync(ct);

        // Hand back the fresh aggregated view — skipping the active-only
        // filter the public query applies so the admin UI can preview edits
        // while the club is still in Pending / Suspended status.
        var memberCount = await _db.Members
            .CountAsync(m => m.AffiliatedClubId == club.Id, ct);
        return new ClubMicrositeDetails(
            club.Id, club.Slug, club.Name, club.City, club.Status,
            club.ContactEmail, club.ContactPhone, club.WebsiteUrl, club.LogoUrl, club.Description,
            club.MicrositeHeadline, club.MicrositeAbout, club.MicrositeHeroImageUrl,
            club.MicrositePrimaryColor, club.MicrositeInstagramHandle,
            club.MicrositeTwitterHandle, club.MicrositeYoutubeChannel,
            memberCount,
            Array.Empty<ClubMicrositeRosterMember>(),
            Array.Empty<ClubMicrositeUpcomingEvent>());
    }
}
