using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Notifications;

public sealed record SendNotificationCommand(
    NotificationChannel Channel,
    string To,
    string Subject,
    string Body) : IRequest<SendNotificationResult>;

public sealed record SendNotificationResult(bool Delivered, string? ProviderMessageId, string? Error);

public sealed class SendNotificationCommandValidator : AbstractValidator<SendNotificationCommand>
{
    public SendNotificationCommandValidator()
    {
        RuleFor(x => x.Channel).IsInEnum();
        RuleFor(x => x.To).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
    }
}

public sealed class SendNotificationCommandHandler
    : IRequestHandler<SendNotificationCommand, SendNotificationResult>
{
    private readonly INotificationService _notifications;

    public SendNotificationCommandHandler(INotificationService notifications)
        => _notifications = notifications;

    public async Task<SendNotificationResult> Handle(
        SendNotificationCommand request, CancellationToken ct)
    {
        var result = await _notifications.SendAsync(
            new NotificationMessage(request.Channel, request.To, request.Subject, request.Body),
            ct);
        return new SendNotificationResult(result.Delivered, result.ProviderMessageId, result.Error);
    }
}

// ─── Campaign: broadcast to all members matching a role/status filter ───────

public sealed record SendCampaignCommand(
    NotificationChannel Channel,
    string Subject,
    string Body,
    MemberRole? Role,
    RegistrationStatus? Status) : IRequest<SendCampaignResult>;

public sealed record SendCampaignResult(int TargetCount, int Delivered, int Failed);

public sealed class SendCampaignCommandValidator : AbstractValidator<SendCampaignCommand>
{
    public SendCampaignCommandValidator()
    {
        RuleFor(x => x.Channel).IsInEnum();
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
    }
}

public sealed class SendCampaignCommandHandler
    : IRequestHandler<SendCampaignCommand, SendCampaignResult>
{
    private readonly IApplicationDbContext _db;
    private readonly INotificationService _notifications;

    public SendCampaignCommandHandler(IApplicationDbContext db, INotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    public async Task<SendCampaignResult> Handle(SendCampaignCommand request, CancellationToken ct)
    {
        var query = _db.Members.AsNoTracking();
        if (request.Role is { } r) query = query.Where(m => m.Role == r);
        if (request.Status is { } s) query = query.Where(m => m.RegistrationStatus == s);

        // We pick the contact field for the channel — email addresses for
        // Email+Push (push falls back to email for dev), phone number for SMS.
        var recipients = await query
            .Select(m => new { m.Email, m.PhoneNumber })
            .ToListAsync(ct);

        var delivered = 0;
        var failed = 0;
        foreach (var recipient in recipients)
        {
            var addr = request.Channel == NotificationChannel.Sms
                ? recipient.PhoneNumber
                : recipient.Email;
            if (string.IsNullOrWhiteSpace(addr)) { failed++; continue; }

            var result = await _notifications.SendAsync(
                new NotificationMessage(request.Channel, addr, request.Subject, request.Body),
                ct);

            if (result.Delivered) delivered++; else failed++;
        }

        return new SendCampaignResult(recipients.Count, delivered, failed);
    }
}
