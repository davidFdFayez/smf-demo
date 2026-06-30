using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;
using SMF.Application.Features.Communication.Common;
using SMF.Domain.Entities;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Communication.Social;

// =============================================================
// Create / update / delete a curated social highlight.
// =============================================================

public sealed record CreateSocialHighlightCommand(
    SocialPlatform Platform,
    string Caption,
    string ExternalUrl,
    string? EmbedHtml,
    string? MediaUrl,
    int DisplayOrder,
    bool IsPublished,
    DateTime? PostedAtUtc) : IRequest<Guid>;

public sealed class CreateSocialHighlightCommandValidator : AbstractValidator<CreateSocialHighlightCommand>
{
    public CreateSocialHighlightCommandValidator()
    {
        RuleFor(x => x.Platform).IsInEnum();
        RuleFor(x => x.Caption).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ExternalUrl).NotEmpty().MaximumLength(1024)
            .Must(u => Uri.TryCreate(u, UriKind.Absolute, out _))
            .WithMessage("ExternalUrl must be an absolute URL.");
        RuleFor(x => x.EmbedHtml).MaximumLength(8000);
        RuleFor(x => x.MediaUrl).MaximumLength(1024);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateSocialHighlightCommandHandler
    : IRequestHandler<CreateSocialHighlightCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public CreateSocialHighlightCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Guid> Handle(CreateSocialHighlightCommand request, CancellationToken ct)
    {
        var highlight = SocialHighlight.Create(
            request.Platform, request.Caption, request.ExternalUrl,
            request.EmbedHtml, request.MediaUrl,
            request.DisplayOrder, request.IsPublished, request.PostedAtUtc,
            _clock.UtcNow);

        _db.SocialHighlights.Add(highlight);
        await _db.SaveChangesAsync(ct);
        return highlight.Id;
    }
}

public sealed record UpdateSocialHighlightCommand(
    Guid Id,
    SocialPlatform Platform,
    string Caption,
    string ExternalUrl,
    string? EmbedHtml,
    string? MediaUrl,
    int DisplayOrder,
    bool IsPublished,
    DateTime? PostedAtUtc) : IRequest<Unit>;

public sealed class UpdateSocialHighlightCommandValidator : AbstractValidator<UpdateSocialHighlightCommand>
{
    public UpdateSocialHighlightCommandValidator()
    {
        RuleFor(x => x.Id).NotEqual(Guid.Empty);
        RuleFor(x => x.Platform).IsInEnum();
        RuleFor(x => x.Caption).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ExternalUrl).NotEmpty().MaximumLength(1024)
            .Must(u => Uri.TryCreate(u, UriKind.Absolute, out _))
            .WithMessage("ExternalUrl must be an absolute URL.");
        RuleFor(x => x.EmbedHtml).MaximumLength(8000);
        RuleFor(x => x.MediaUrl).MaximumLength(1024);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateSocialHighlightCommandHandler : IRequestHandler<UpdateSocialHighlightCommand, Unit>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public UpdateSocialHighlightCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Unit> Handle(UpdateSocialHighlightCommand request, CancellationToken ct)
    {
        var highlight = await _db.SocialHighlights.FirstOrDefaultAsync(h => h.Id == request.Id, ct);
        if (highlight is null) throw new NotFoundException(nameof(SocialHighlight), request.Id);

        highlight.Update(
            request.Platform, request.Caption, request.ExternalUrl,
            request.EmbedHtml, request.MediaUrl,
            request.DisplayOrder, request.IsPublished, request.PostedAtUtc,
            _clock.UtcNow);

        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed record DeleteSocialHighlightCommand(Guid Id) : IRequest<Unit>;

public sealed class DeleteSocialHighlightCommandHandler : IRequestHandler<DeleteSocialHighlightCommand, Unit>
{
    private readonly IApplicationDbContext _db;

    public DeleteSocialHighlightCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Unit> Handle(DeleteSocialHighlightCommand request, CancellationToken ct)
    {
        var highlight = await _db.SocialHighlights.FirstOrDefaultAsync(h => h.Id == request.Id, ct);
        if (highlight is null) throw new NotFoundException(nameof(SocialHighlight), request.Id);

        _db.SocialHighlights.Remove(highlight);
        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// =============================================================
// Queries — public surface returns published only; admin returns all.
// =============================================================

public sealed record ListSocialHighlightsQuery(
    bool IncludeUnpublished = false,
    SocialPlatform? Platform = null,
    int? Take = null) : IRequest<IReadOnlyList<SocialHighlightDto>>;

public sealed class ListSocialHighlightsQueryHandler
    : IRequestHandler<ListSocialHighlightsQuery, IReadOnlyList<SocialHighlightDto>>
{
    private readonly IApplicationDbContext _db;

    public ListSocialHighlightsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<SocialHighlightDto>> Handle(
        ListSocialHighlightsQuery request, CancellationToken ct)
    {
        var q = _db.SocialHighlights.AsNoTracking().AsQueryable();
        if (!request.IncludeUnpublished) q = q.Where(h => h.IsPublished);
        if (request.Platform is { } p)   q = q.Where(h => h.Platform == p);

        q = q.OrderBy(h => h.DisplayOrder).ThenByDescending(h => h.PostedAtUtc ?? h.CreatedAtUtc);
        if (request.Take is { } take) q = q.Take(Math.Clamp(take, 1, 200));

        return await q.Select(h => new SocialHighlightDto(
            h.Id, h.Platform, h.Caption, h.ExternalUrl,
            h.EmbedHtml, h.MediaUrl, h.DisplayOrder, h.IsPublished,
            h.PostedAtUtc, h.CreatedAtUtc))
            .ToListAsync(ct);
    }
}
