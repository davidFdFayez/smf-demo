using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Entities;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Safeguarding;

// ─── DTOs ───────────────────────────────────────────────────────────────────

public sealed record SafeguardingReportSummary(
    Guid Id,
    string ReferenceCode,
    SafeguardingCategory Category,
    string Subject,
    SafeguardingReportStatus Status,
    bool IsAnonymous,
    DateTime SubmittedAtUtc,
    DateTime? ResolvedAtUtc);

public sealed record SafeguardingReportDetails(
    Guid Id,
    string ReferenceCode,
    SafeguardingCategory Category,
    string Subject,
    string Description,
    string? IncidentLocation,
    DateOnly? IncidentDate,
    bool IsAnonymous,
    string? ReporterName,
    string? ReporterEmail,
    string? ReporterPhone,
    SafeguardingReportStatus Status,
    string? ReviewerNotes,
    DateTime SubmittedAtUtc,
    DateTime? ResolvedAtUtc);

internal static class SafeguardingMapper
{
    public static SafeguardingReportDetails ToDetails(SafeguardingReport r) => new(
        r.Id, r.ReferenceCode, r.Category, r.Subject, r.Description,
        r.IncidentLocation, r.IncidentDate, r.IsAnonymous,
        r.ReporterName, r.ReporterEmail, r.ReporterPhone,
        r.Status, r.ReviewerNotes,
        r.SubmittedAtUtc, r.ResolvedAtUtc);
}

// ─── Submit ─────────────────────────────────────────────────────────────────

public sealed record SubmitSafeguardingReportCommand(
    SafeguardingCategory Category,
    string Subject,
    string Description,
    string? IncidentLocation,
    DateOnly? IncidentDate,
    bool IsAnonymous,
    string? ReporterName,
    string? ReporterEmail,
    string? ReporterPhone) : IRequest<SafeguardingReportDetails>;

public sealed class SubmitSafeguardingReportCommandValidator
    : AbstractValidator<SubmitSafeguardingReportCommand>
{
    public SubmitSafeguardingReportCommandValidator()
    {
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(5000);
        RuleFor(x => x.IncidentLocation).MaximumLength(200);
        RuleFor(x => x.ReporterName).MaximumLength(200);
        RuleFor(x => x.ReporterEmail)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.ReporterEmail));
        RuleFor(x => x.ReporterPhone).MaximumLength(32);

        RuleFor(x => x)
            .Must(c => c.IsAnonymous
                      || !string.IsNullOrWhiteSpace(c.ReporterEmail)
                      || !string.IsNullOrWhiteSpace(c.ReporterPhone))
            .WithMessage(
                "Non-anonymous reports must include at least an email or phone number.");
    }
}

public sealed class SubmitSafeguardingReportCommandHandler
    : IRequestHandler<SubmitSafeguardingReportCommand, SafeguardingReportDetails>
{
    private readonly IApplicationDbContext _db;

    public SubmitSafeguardingReportCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<SafeguardingReportDetails> Handle(
        SubmitSafeguardingReportCommand request,
        CancellationToken cancellationToken)
    {
        var report = SafeguardingReport.Submit(
            request.Category,
            request.Subject,
            request.Description,
            request.IncidentLocation,
            request.IncidentDate,
            request.IsAnonymous,
            request.ReporterName,
            request.ReporterEmail,
            request.ReporterPhone);

        _db.SafeguardingReports.Add(report);
        await _db.SaveChangesAsync(cancellationToken);
        return SafeguardingMapper.ToDetails(report);
    }
}

// ─── Triage (admin actions) ─────────────────────────────────────────────────

public sealed record TriageSafeguardingReportCommand(
    Guid Id,
    SafeguardingReportStatus NewStatus,
    string? ReviewerNotes) : IRequest<SafeguardingReportDetails>;

public sealed class TriageSafeguardingReportCommandHandler
    : IRequestHandler<TriageSafeguardingReportCommand, SafeguardingReportDetails>
{
    private readonly IApplicationDbContext _db;

    public TriageSafeguardingReportCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<SafeguardingReportDetails> Handle(
        TriageSafeguardingReportCommand request,
        CancellationToken cancellationToken)
    {
        var report = await _db.SafeguardingReports
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SafeguardingReport), request.Id);

        switch (request.NewStatus)
        {
            case SafeguardingReportStatus.UnderReview:
                report.MarkUnderReview(request.ReviewerNotes);
                break;
            case SafeguardingReportStatus.Resolved:
                report.Resolve(request.ReviewerNotes, dismissed: false);
                break;
            case SafeguardingReportStatus.Dismissed:
                report.Resolve(request.ReviewerNotes, dismissed: true);
                break;
            case SafeguardingReportStatus.Submitted:
            default:
                throw new InvalidOperationException(
                    "Cannot transition report back to Submitted.");
        }

        await _db.SaveChangesAsync(cancellationToken);
        return SafeguardingMapper.ToDetails(report);
    }
}

// ─── Queries ────────────────────────────────────────────────────────────────

public sealed record ListSafeguardingReportsQuery(
    SafeguardingReportStatus? Status,
    int Limit = 100) : IRequest<IReadOnlyList<SafeguardingReportSummary>>;

public sealed class ListSafeguardingReportsQueryHandler
    : IRequestHandler<ListSafeguardingReportsQuery, IReadOnlyList<SafeguardingReportSummary>>
{
    private readonly IApplicationDbContext _db;

    public ListSafeguardingReportsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<SafeguardingReportSummary>> Handle(
        ListSafeguardingReportsQuery request,
        CancellationToken cancellationToken)
    {
        var q = _db.SafeguardingReports.AsNoTracking();
        if (request.Status is { } s) q = q.Where(r => r.Status == s);

        var limit = Math.Clamp(request.Limit, 1, 500);
        return await q
            .OrderByDescending(r => r.SubmittedAtUtc)
            .Take(limit)
            .Select(r => new SafeguardingReportSummary(
                r.Id, r.ReferenceCode, r.Category, r.Subject, r.Status,
                r.IsAnonymous, r.SubmittedAtUtc, r.ResolvedAtUtc))
            .ToListAsync(cancellationToken);
    }
}

public sealed record GetSafeguardingReportByCodeQuery(string ReferenceCode)
    : IRequest<SafeguardingReportDetails>;

public sealed class GetSafeguardingReportByCodeQueryHandler
    : IRequestHandler<GetSafeguardingReportByCodeQuery, SafeguardingReportDetails>
{
    private readonly IApplicationDbContext _db;

    public GetSafeguardingReportByCodeQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<SafeguardingReportDetails> Handle(
        GetSafeguardingReportByCodeQuery request,
        CancellationToken cancellationToken)
    {
        var code = (request.ReferenceCode ?? string.Empty).Trim().ToUpperInvariant();

        var report = await _db.SafeguardingReports
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ReferenceCode == code, cancellationToken)
            ?? throw new NotFoundException(nameof(SafeguardingReport), code);

        return SafeguardingMapper.ToDetails(report);
    }
}
