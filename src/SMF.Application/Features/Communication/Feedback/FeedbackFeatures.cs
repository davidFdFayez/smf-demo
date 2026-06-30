using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;
using SMF.Application.Features.Communication.Common;
using SMF.Application.Features.Members.Queries.GetMembers;
using SMF.Domain.Entities;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Communication.Feedback;

// =============================================================
// Submit feedback — works for both authenticated members and guests.
// Always lands as Pending; an admin promotes to Public/Hidden.
// =============================================================

public sealed record SubmitFeedbackCommand(
    FeedbackSubjectType SubjectType,
    Guid? SubjectId,
    int Rating,
    string Comment,
    Guid? AuthorMemberId,
    string AuthorName,
    string? AuthorEmail) : IRequest<FeedbackDto>;

public sealed class SubmitFeedbackCommandValidator : AbstractValidator<SubmitFeedbackCommand>
{
    public SubmitFeedbackCommandValidator()
    {
        RuleFor(x => x.SubjectType).IsInEnum();
        RuleFor(x => x.SubjectId)
            .NotEqual(Guid.Empty)
            .When(x => x.SubjectType != FeedbackSubjectType.General);
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Comment).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.AuthorName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AuthorEmail).EmailAddress().MaximumLength(320)
            .When(x => !string.IsNullOrWhiteSpace(x.AuthorEmail));
    }
}

public sealed class SubmitFeedbackCommandHandler : IRequestHandler<SubmitFeedbackCommand, FeedbackDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public SubmitFeedbackCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<FeedbackDto> Handle(SubmitFeedbackCommand request, CancellationToken ct)
    {
        // Cross-check the subject row actually exists when one is required.
        if (request.SubjectType != FeedbackSubjectType.General && request.SubjectId is { } sid)
        {
            var exists = request.SubjectType switch
            {
                FeedbackSubjectType.Event   => await _db.Events.AnyAsync(e => e.Id == sid, ct),
                FeedbackSubjectType.Product => await _db.Products.AnyAsync(p => p.Id == sid, ct),
                FeedbackSubjectType.Course  => await _db.Courses.AnyAsync(c => c.Id == sid, ct),
                _ => true
            };
            if (!exists)
                throw new NotFoundException($"{request.SubjectType}", sid);
        }

        var feedback = Domain.Entities.Feedback.Submit(
            request.SubjectType,
            request.SubjectId,
            request.Rating,
            request.Comment,
            request.AuthorMemberId,
            request.AuthorName,
            request.AuthorEmail,
            _clock.UtcNow);

        _db.Feedbacks.Add(feedback);
        await _db.SaveChangesAsync(ct);

        return Map(feedback);
    }

    internal static FeedbackDto Map(Domain.Entities.Feedback f) => new(
        f.Id, f.SubjectType, f.SubjectId, f.Rating, f.Comment,
        f.AuthorMemberId, f.AuthorName, f.AuthorEmail,
        f.Status, f.AdminNotes, f.CreatedAtUtc, f.ModeratedAtUtc);
}

// =============================================================
// Moderate (publish or hide) — admin only.
// =============================================================

public sealed record ModerateFeedbackCommand(
    Guid FeedbackId,
    FeedbackStatus NewStatus,
    Guid? ModeratorMemberId,
    string? AdminNotes) : IRequest<FeedbackDto>;

public sealed class ModerateFeedbackCommandValidator : AbstractValidator<ModerateFeedbackCommand>
{
    public ModerateFeedbackCommandValidator()
    {
        RuleFor(x => x.FeedbackId).NotEqual(Guid.Empty);
        RuleFor(x => x.NewStatus).Must(s => s != FeedbackStatus.Pending)
            .WithMessage("Cannot moderate back to Pending.");
        RuleFor(x => x.AdminNotes).MaximumLength(1000);
    }
}

public sealed class ModerateFeedbackCommandHandler : IRequestHandler<ModerateFeedbackCommand, FeedbackDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public ModerateFeedbackCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<FeedbackDto> Handle(ModerateFeedbackCommand request, CancellationToken ct)
    {
        var feedback = await _db.Feedbacks.FirstOrDefaultAsync(f => f.Id == request.FeedbackId, ct);
        if (feedback is null) throw new NotFoundException(nameof(Domain.Entities.Feedback), request.FeedbackId);

        feedback.Moderate(request.NewStatus, request.ModeratorMemberId, request.AdminNotes, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        return SubmitFeedbackCommandHandler.Map(feedback);
    }
}

// =============================================================
// Queries — public ("public" = Status==Public) and admin (no filter).
// =============================================================

public sealed record ListFeedbackQuery(
    int Page = 1,
    int PageSize = 20,
    FeedbackSubjectType? SubjectType = null,
    Guid? SubjectId = null,
    FeedbackStatus? Status = null,
    int? MinRating = null) : IRequest<PagedResult<FeedbackDto>>;

public sealed class ListFeedbackQueryHandler : IRequestHandler<ListFeedbackQuery, PagedResult<FeedbackDto>>
{
    private readonly IApplicationDbContext _db;

    public ListFeedbackQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PagedResult<FeedbackDto>> Handle(ListFeedbackQuery request, CancellationToken ct)
    {
        var page     = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var q = _db.Feedbacks.AsNoTracking().AsQueryable();
        if (request.SubjectType is { } st) q = q.Where(f => f.SubjectType == st);
        if (request.SubjectId   is { } sid) q = q.Where(f => f.SubjectId == sid);
        if (request.Status      is { } s) q = q.Where(f => f.Status == s);
        if (request.MinRating   is { } mr) q = q.Where(f => f.Rating >= mr);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(f => f.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(f => new FeedbackDto(
                f.Id, f.SubjectType, f.SubjectId, f.Rating, f.Comment,
                f.AuthorMemberId, f.AuthorName, f.AuthorEmail,
                f.Status, f.AdminNotes, f.CreatedAtUtc, f.ModeratedAtUtc))
            .ToListAsync(ct);

        return new PagedResult<FeedbackDto>(items, total, page, pageSize);
    }
}

public sealed record FeedbackStatsQuery(
    FeedbackSubjectType? SubjectType = null,
    Guid? SubjectId = null) : IRequest<FeedbackStatsDto>;

public sealed record FeedbackStatsDto(int Count, double AverageRating);

public sealed class FeedbackStatsQueryHandler : IRequestHandler<FeedbackStatsQuery, FeedbackStatsDto>
{
    private readonly IApplicationDbContext _db;

    public FeedbackStatsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<FeedbackStatsDto> Handle(FeedbackStatsQuery request, CancellationToken ct)
    {
        var q = _db.Feedbacks.AsNoTracking()
            .Where(f => f.Status == FeedbackStatus.Public);
        if (request.SubjectType is { } st) q = q.Where(f => f.SubjectType == st);
        if (request.SubjectId   is { } sid) q = q.Where(f => f.SubjectId == sid);

        var data = await q.GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Avg = g.Average(f => (double)f.Rating) })
            .FirstOrDefaultAsync(ct);

        return data is null
            ? new FeedbackStatsDto(0, 0)
            : new FeedbackStatsDto(data.Count, Math.Round(data.Avg, 2));
    }
}
