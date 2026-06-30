using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Entities;
using SMF.Domain.Enums;

namespace SMF.Application.Features.News;

// ─── DTOs ───────────────────────────────────────────────────────────────────

public sealed record NewsArticleSummary(
    Guid Id,
    string Title,
    string Slug,
    string Summary,
    NewsCategory Category,
    string? CoverImageUrl,
    string AuthorDisplayName,
    bool IsPublished,
    bool IsArchived,
    DateTime CreatedAtUtc,
    DateTime? PublishedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record NewsArticleDetails(
    Guid Id,
    string Title,
    string Slug,
    string Summary,
    string Body,
    NewsCategory Category,
    string? CoverImageUrl,
    string AuthorDisplayName,
    bool IsPublished,
    bool IsArchived,
    DateTime CreatedAtUtc,
    DateTime? PublishedAtUtc,
    DateTime? UpdatedAtUtc);

internal static class NewsMapper
{
    public static NewsArticleSummary ToSummary(NewsArticle n) => new(
        n.Id, n.Title, n.Slug, n.Summary, n.Category, n.CoverImageUrl,
        n.AuthorDisplayName, n.IsPublished, n.IsArchived,
        n.CreatedAtUtc, n.PublishedAtUtc, n.UpdatedAtUtc);

    public static NewsArticleDetails ToDetails(NewsArticle n) => new(
        n.Id, n.Title, n.Slug, n.Summary, n.Body, n.Category, n.CoverImageUrl,
        n.AuthorDisplayName, n.IsPublished, n.IsArchived,
        n.CreatedAtUtc, n.PublishedAtUtc, n.UpdatedAtUtc);
}

// ─── Create (as draft) ──────────────────────────────────────────────────────

public sealed record CreateNewsArticleCommand(
    string Title,
    string Summary,
    string Body,
    NewsCategory Category,
    string AuthorDisplayName,
    string? CoverImageUrl) : IRequest<NewsArticleDetails>;

public sealed class CreateNewsArticleCommandValidator : AbstractValidator<CreateNewsArticleCommand>
{
    public CreateNewsArticleCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Summary).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(20_000);
        RuleFor(x => x.AuthorDisplayName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.CoverImageUrl).MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.CoverImageUrl));
    }
}

public sealed class CreateNewsArticleCommandHandler
    : IRequestHandler<CreateNewsArticleCommand, NewsArticleDetails>
{
    private readonly IApplicationDbContext _db;

    public CreateNewsArticleCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<NewsArticleDetails> Handle(
        CreateNewsArticleCommand request,
        CancellationToken cancellationToken)
    {
        var article = NewsArticle.Draft(
            request.Title,
            request.Summary,
            request.Body,
            request.Category,
            request.AuthorDisplayName,
            request.CoverImageUrl);

        // Handle slug collisions defensively: another article may already
        // have the same slug, so probe and apply a numeric suffix.
        var baseSlug = article.Slug;
        var suffix = 1;
        while (await _db.NewsArticles.AnyAsync(
                   a => a.Slug == article.Slug, cancellationToken))
        {
            article.ApplySlugSuffix(suffix++);
            if (suffix > 500)
                throw new InvalidOperationException(
                    $"Unable to allocate unique slug for '{baseSlug}'.");
        }

        _db.NewsArticles.Add(article);
        await _db.SaveChangesAsync(cancellationToken);
        return NewsMapper.ToDetails(article);
    }
}

// ─── Update (CMS editor) ────────────────────────────────────────────────────

public sealed record UpdateNewsArticleCommand(
    Guid Id,
    string Title,
    string Summary,
    string Body,
    NewsCategory Category,
    string? CoverImageUrl) : IRequest<NewsArticleDetails>;

public sealed class UpdateNewsArticleCommandValidator : AbstractValidator<UpdateNewsArticleCommand>
{
    public UpdateNewsArticleCommandValidator()
    {
        RuleFor(x => x.Id).NotEqual(Guid.Empty);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Summary).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(20_000);
        RuleFor(x => x.CoverImageUrl).MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.CoverImageUrl));
    }
}

public sealed class UpdateNewsArticleCommandHandler
    : IRequestHandler<UpdateNewsArticleCommand, NewsArticleDetails>
{
    private readonly IApplicationDbContext _db;
    public UpdateNewsArticleCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<NewsArticleDetails> Handle(UpdateNewsArticleCommand request, CancellationToken ct)
    {
        var article = await _db.NewsArticles.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new NotFoundException(nameof(NewsArticle), request.Id);

        article.UpdateContent(
            request.Title, request.Summary, request.Body,
            request.Category, request.CoverImageUrl);

        await _db.SaveChangesAsync(ct);
        return NewsMapper.ToDetails(article);
    }
}

// ─── Publish ─────────────────────────────────────────────────────────────────

public sealed record PublishNewsArticleCommand(Guid Id) : IRequest<NewsArticleDetails>;

public sealed class PublishNewsArticleCommandHandler
    : IRequestHandler<PublishNewsArticleCommand, NewsArticleDetails>
{
    private readonly IApplicationDbContext _db;

    public PublishNewsArticleCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<NewsArticleDetails> Handle(
        PublishNewsArticleCommand request,
        CancellationToken cancellationToken)
    {
        var article = await _db.NewsArticles
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(NewsArticle), request.Id);

        article.Publish();
        await _db.SaveChangesAsync(cancellationToken);
        return NewsMapper.ToDetails(article);
    }
}

// ─── Archive ────────────────────────────────────────────────────────────────

public sealed record ArchiveNewsArticleCommand(Guid Id) : IRequest<Unit>;

public sealed class ArchiveNewsArticleCommandHandler
    : IRequestHandler<ArchiveNewsArticleCommand, Unit>
{
    private readonly IApplicationDbContext _db;

    public ArchiveNewsArticleCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Unit> Handle(
        ArchiveNewsArticleCommand request,
        CancellationToken cancellationToken)
    {
        var article = await _db.NewsArticles
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(NewsArticle), request.Id);

        article.Archive();
        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

// ─── Queries ────────────────────────────────────────────────────────────────

public sealed record ListPublishedNewsQuery(
    NewsCategory? Category,
    int Limit = 20) : IRequest<IReadOnlyList<NewsArticleSummary>>;

public sealed class ListPublishedNewsQueryHandler
    : IRequestHandler<ListPublishedNewsQuery, IReadOnlyList<NewsArticleSummary>>
{
    private readonly IApplicationDbContext _db;

    public ListPublishedNewsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<NewsArticleSummary>> Handle(
        ListPublishedNewsQuery request,
        CancellationToken cancellationToken)
    {
        var q = _db.NewsArticles
            .AsNoTracking()
            .Where(a => a.IsPublished && !a.IsArchived);

        if (request.Category is { } c) q = q.Where(a => a.Category == c);

        var limit = Math.Clamp(request.Limit, 1, 100);

        return await q
            .OrderByDescending(a => a.PublishedAtUtc)
            .Take(limit)
            .Select(a => NewsMapper.ToSummary(a))
            .ToListAsync(cancellationToken);
    }
}

public sealed record ListAllNewsQuery(int Limit = 100) : IRequest<IReadOnlyList<NewsArticleSummary>>;

public sealed class ListAllNewsQueryHandler
    : IRequestHandler<ListAllNewsQuery, IReadOnlyList<NewsArticleSummary>>
{
    private readonly IApplicationDbContext _db;

    public ListAllNewsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<NewsArticleSummary>> Handle(
        ListAllNewsQuery request,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(request.Limit, 1, 500);
        return await _db.NewsArticles
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(limit)
            .Select(a => NewsMapper.ToSummary(a))
            .ToListAsync(cancellationToken);
    }
}

/// <summary>
/// Fetch a news article by its URL slug. <see cref="IncludeUnpublished"/>
/// defaults to <c>false</c> so public routes never accidentally leak a
/// draft or archived article — admin endpoints opt in explicitly.
/// </summary>
public sealed record GetNewsBySlugQuery(string Slug, bool IncludeUnpublished = false)
    : IRequest<NewsArticleDetails>;

public sealed class GetNewsBySlugQueryHandler
    : IRequestHandler<GetNewsBySlugQuery, NewsArticleDetails>
{
    private readonly IApplicationDbContext _db;

    public GetNewsBySlugQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<NewsArticleDetails> Handle(
        GetNewsBySlugQuery request,
        CancellationToken cancellationToken)
    {
        var q = _db.NewsArticles.AsNoTracking().Where(a => a.Slug == request.Slug);
        if (!request.IncludeUnpublished)
            q = q.Where(a => a.IsPublished && !a.IsArchived);

        var article = await q.FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(NewsArticle), request.Slug);

        return NewsMapper.ToDetails(article);
    }
}
