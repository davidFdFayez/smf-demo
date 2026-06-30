using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;
using SMF.Application.Features.Communication.Common;
using SMF.Application.Features.Members.Queries.GetMembers;
using SMF.Domain.Entities;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Communication.Broadcasts;

// =============================================================
// Create a campaign (Draft if no schedule, Scheduled otherwise).
// =============================================================

public sealed record CreateBroadcastCommand(
    string Title,
    string Subject,
    string Body,
    BroadcastChannel Channels,
    IReadOnlyList<MemberRole>? TargetRoles,
    Guid? TargetClubId,
    Guid? TargetEventId,
    bool ActiveMembersOnly,
    Guid? CreatedByMemberId = null,
    DateTime? ScheduledAtUtc = null) : IRequest<Guid>;

public sealed class CreateBroadcastCommandValidator : AbstractValidator<CreateBroadcastCommand>
{
    public CreateBroadcastCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(8000);
        RuleFor(x => x.Channels)
            .Must(c => (c & BroadcastChannel.All) != BroadcastChannel.None)
            .WithMessage("At least one delivery channel must be selected.");
    }
}

public sealed class CreateBroadcastCommandHandler : IRequestHandler<CreateBroadcastCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public CreateBroadcastCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Guid> Handle(CreateBroadcastCommand request, CancellationToken ct)
    {
        var campaign = BroadcastCampaign.Create(
            request.Title, request.Subject, request.Body,
            request.Channels,
            request.TargetRoles,
            request.TargetClubId,
            request.TargetEventId,
            request.ActiveMembersOnly,
            request.CreatedByMemberId,
            request.ScheduledAtUtc,
            _clock.UtcNow);

        _db.BroadcastCampaigns.Add(campaign);
        await _db.SaveChangesAsync(ct);
        return campaign.Id;
    }
}

// =============================================================
// Send a campaign now — synchronous fan-out via INotificationDispatcher.
// We deliberately keep this synchronous (not a hosted-service crank) so the
// admin UI can show a progress / completion summary in-line. Bigger
// federations can later move the loop into a hosted service without
// changing the API surface.
// =============================================================

public sealed record SendBroadcastCommand(Guid CampaignId) : IRequest<BroadcastDetailDto>;

public sealed class SendBroadcastCommandHandler : IRequestHandler<SendBroadcastCommand, BroadcastDetailDto>
{
    private readonly IApplicationDbContext _db;
    private readonly INotificationDispatcher _dispatcher;
    private readonly BroadcastAudienceResolver _audience;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<SendBroadcastCommandHandler> _logger;

    public SendBroadcastCommandHandler(
        IApplicationDbContext db,
        INotificationDispatcher dispatcher,
        BroadcastAudienceResolver audience,
        IDateTimeProvider clock,
        ILogger<SendBroadcastCommandHandler> logger)
    {
        _db = db;
        _dispatcher = dispatcher;
        _audience = audience;
        _clock = clock;
        _logger = logger;
    }

    public async Task<BroadcastDetailDto> Handle(SendBroadcastCommand request, CancellationToken ct)
    {
        var campaign = await _db.BroadcastCampaigns
            .FirstOrDefaultAsync(c => c.Id == request.CampaignId, ct);
        if (campaign is null) throw new NotFoundException(nameof(BroadcastCampaign), request.CampaignId);

        if (campaign.Status is BroadcastStatus.Sent or BroadcastStatus.Sending)
            throw new InvalidOperationException(
                $"Campaign is already in status '{campaign.Status}'.");

        var recipients = await _audience.ResolveAsync(campaign, ct);
        if (recipients.Count == 0)
        {
            campaign.MarkFailed("Audience filter resolved to zero recipients.", _clock.UtcNow);
            await _db.SaveChangesAsync(ct);
            return MapDetail(campaign);
        }

        campaign.MarkSending(recipients.Count, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        try
        {
            await _dispatcher.DispatchAsync(campaign, recipients, ct);
            campaign.MarkComplete(_clock.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Broadcast campaign {Id} dispatch failed", campaign.Id);
            campaign.MarkFailed(ex.Message, _clock.UtcNow);
        }

        await _db.SaveChangesAsync(ct);
        return MapDetail(campaign);
    }

    private static BroadcastDetailDto MapDetail(BroadcastCampaign c) => new(
        c.Id, c.Title, c.Subject, c.Body, c.Channels,
        c.ResolveTargetRoles().ToList(),
        c.TargetClubId, c.TargetEventId, c.ActiveMembersOnly,
        c.Status, c.TotalTargets, c.DeliveredCount, c.FailedCount,
        c.FailureReason, c.CreatedByMemberId,
        c.CreatedAtUtc, c.ScheduledAtUtc, c.StartedAtUtc, c.CompletedAtUtc);
}

// =============================================================
// Cancel a draft / scheduled campaign.
// =============================================================

public sealed record CancelBroadcastCommand(Guid CampaignId) : IRequest<Unit>;

public sealed class CancelBroadcastCommandHandler : IRequestHandler<CancelBroadcastCommand, Unit>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public CancelBroadcastCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Unit> Handle(CancelBroadcastCommand request, CancellationToken ct)
    {
        var campaign = await _db.BroadcastCampaigns.FirstOrDefaultAsync(c => c.Id == request.CampaignId, ct);
        if (campaign is null) throw new NotFoundException(nameof(BroadcastCampaign), request.CampaignId);

        campaign.Cancel(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// =============================================================
// Queries
// =============================================================

public sealed record ListBroadcastsQuery(
    int Page = 1,
    int PageSize = 25,
    BroadcastStatus? Status = null,
    string? Search = null) : IRequest<PagedResult<BroadcastSummaryDto>>;

public sealed class ListBroadcastsQueryHandler
    : IRequestHandler<ListBroadcastsQuery, PagedResult<BroadcastSummaryDto>>
{
    private readonly IApplicationDbContext _db;

    public ListBroadcastsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PagedResult<BroadcastSummaryDto>> Handle(ListBroadcastsQuery request, CancellationToken ct)
    {
        var page     = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var q = _db.BroadcastCampaigns.AsNoTracking().AsQueryable();
        if (request.Status is { } s) q = q.Where(c => c.Status == s);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            q = q.Where(c => c.Title.Contains(term) || c.Subject.Contains(term));
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(c => c.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => new BroadcastSummaryDto(
                c.Id, c.Title, c.Subject, c.Channels, c.Status,
                c.TotalTargets, c.DeliveredCount, c.FailedCount,
                c.CreatedAtUtc, c.ScheduledAtUtc, c.CompletedAtUtc))
            .ToListAsync(ct);

        return new PagedResult<BroadcastSummaryDto>(items, total, page, pageSize);
    }
}

public sealed record GetBroadcastQuery(Guid Id) : IRequest<BroadcastDetailDto>;

public sealed class GetBroadcastQueryHandler : IRequestHandler<GetBroadcastQuery, BroadcastDetailDto>
{
    private readonly IApplicationDbContext _db;

    public GetBroadcastQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<BroadcastDetailDto> Handle(GetBroadcastQuery request, CancellationToken ct)
    {
        var c = await _db.BroadcastCampaigns.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new NotFoundException(nameof(BroadcastCampaign), request.Id);

        return new BroadcastDetailDto(
            c.Id, c.Title, c.Subject, c.Body, c.Channels,
            c.ResolveTargetRoles().ToList(),
            c.TargetClubId, c.TargetEventId, c.ActiveMembersOnly,
            c.Status, c.TotalTargets, c.DeliveredCount, c.FailedCount,
            c.FailureReason, c.CreatedByMemberId,
            c.CreatedAtUtc, c.ScheduledAtUtc, c.StartedAtUtc, c.CompletedAtUtc);
    }
}

// =============================================================
// Audience preview — admin "how many people will this hit?"
// =============================================================

public sealed record PreviewBroadcastAudienceQuery(
    BroadcastChannel Channels,
    IReadOnlyList<MemberRole>? TargetRoles,
    Guid? TargetClubId,
    Guid? TargetEventId,
    bool ActiveMembersOnly) : IRequest<BroadcastAudiencePreview>;

public sealed record BroadcastAudiencePreview(
    int TotalMembers,
    int EmailReachable,
    int SmsReachable,
    int PushReachable);

public sealed class PreviewBroadcastAudienceQueryHandler
    : IRequestHandler<PreviewBroadcastAudienceQuery, BroadcastAudiencePreview>
{
    private readonly IApplicationDbContext _db;
    private readonly BroadcastAudienceResolver _audience;
    private readonly IDateTimeProvider _clock;

    public PreviewBroadcastAudienceQueryHandler(
        IApplicationDbContext db, BroadcastAudienceResolver audience, IDateTimeProvider clock)
    {
        _db = db;
        _audience = audience;
        _clock = clock;
    }

    public async Task<BroadcastAudiencePreview> Handle(
        PreviewBroadcastAudienceQuery request, CancellationToken ct)
    {
        // Build a transient campaign so we can reuse the resolver verbatim.
        var stub = BroadcastCampaign.Create(
            "preview", "preview", "preview",
            request.Channels == BroadcastChannel.None ? BroadcastChannel.All : request.Channels,
            request.TargetRoles,
            request.TargetClubId,
            request.TargetEventId,
            request.ActiveMembersOnly,
            null, null, _clock.UtcNow);

        var recipients = await _audience.ResolveAsync(stub, ct);
        return new BroadcastAudiencePreview(
            recipients.Count,
            recipients.Count(r => !string.IsNullOrWhiteSpace(r.Email)),
            recipients.Count(r => !string.IsNullOrWhiteSpace(r.PhoneE164)),
            recipients.Count(r => r.ActiveDeviceTokens.Count > 0));
    }
}
