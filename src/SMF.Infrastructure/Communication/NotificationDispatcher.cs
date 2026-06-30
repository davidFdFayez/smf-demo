using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SMF.Application.Common.Interfaces;
using SMF.Application.Features.Communication.Broadcasts;
using SMF.Domain.Entities;
using SMF.Domain.Enums;

namespace SMF.Infrastructure.Communication;

/// <summary>
/// Concrete fan-out coordinator for broadcasts and one-off messages.
///
/// Broadcasts iterate over the resolved <see cref="BroadcastRecipient"/>
/// list, persist a queued <see cref="Notification"/> per (recipient × channel)
/// pair, hand the payload to the matching channel sender, then mark the row
/// Sent/Failed. Counters on the <see cref="BroadcastCampaign"/> aggregate are
/// updated in place; the caller is responsible for the final
/// <c>SaveChangesAsync</c>. Direct sends bypass the campaign machinery.
///
/// Stale FCM tokens reported by the push sender are deactivated immediately
/// so the next broadcast doesn't re-send to known-dead devices.
/// </summary>
internal sealed class NotificationDispatcher : INotificationDispatcher, INotificationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEmailSender _email;
    private readonly ISmsSender _sms;
    private readonly IPushSender _push;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        IServiceScopeFactory scopeFactory,
        IEmailSender email,
        ISmsSender sms,
        IPushSender push,
        IDateTimeProvider clock,
        ILogger<NotificationDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _email = email;
        _sms = sms;
        _push = push;
        _clock = clock;
        _logger = logger;
    }

    public async Task DispatchAsync(
        BroadcastCampaign campaign,
        IReadOnlyList<BroadcastRecipient> recipients,
        CancellationToken cancellationToken = default)
    {
        // Use a fresh scope for the dispatch loop so we don't mutate the
        // caller's tracked-entity graph; we still update the campaign
        // counters via the entity reference the caller passed in.
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var batch = 0;
        foreach (var recipient in recipients)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if ((campaign.Channels & BroadcastChannel.Email) == BroadcastChannel.Email
                && !string.IsNullOrWhiteSpace(recipient.Email))
            {
                await DeliverAsync(
                    db, campaign, NotificationChannel.Email,
                    recipient.Email!, recipient.MemberId,
                    campaign.Subject, campaign.Body, cancellationToken);
                batch++;
            }

            if ((campaign.Channels & BroadcastChannel.Sms) == BroadcastChannel.Sms
                && !string.IsNullOrWhiteSpace(recipient.PhoneE164))
            {
                await DeliverAsync(
                    db, campaign, NotificationChannel.Sms,
                    recipient.PhoneE164!, recipient.MemberId,
                    campaign.Subject, campaign.Body, cancellationToken);
                batch++;
            }

            if ((campaign.Channels & BroadcastChannel.Push) == BroadcastChannel.Push
                && recipient.ActiveDeviceTokens.Count > 0)
            {
                foreach (var token in recipient.ActiveDeviceTokens)
                {
                    await DeliverAsync(
                        db, campaign, NotificationChannel.Push,
                        token, recipient.MemberId,
                        campaign.Subject, campaign.Body, cancellationToken);
                    batch++;
                }
            }

            // Save in chunks so a long campaign doesn't run with a giant
            // change tracker.
            if (batch >= 50)
            {
                await db.SaveChangesAsync(cancellationToken);
                batch = 0;
            }
        }

        if (batch > 0)
            await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Notification> SendDirectAsync(
        NotificationChannel channel,
        string recipientAddress,
        string subject,
        string body,
        Guid? memberId,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var notification = Notification.Queue(
            channel, recipientAddress, subject, body, memberId, null, _clock.UtcNow);
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(cancellationToken);

        await DeliverPersistedAsync(db, notification, memberId, cancellationToken, campaign: null);
        await db.SaveChangesAsync(cancellationToken);
        return notification;
    }

    // ── INotificationService legacy adapter (used by existing handlers) ──

    public async Task<NotificationResult> SendAsync(
        NotificationMessage message, CancellationToken cancellationToken = default)
    {
        var n = await SendDirectAsync(
            message.Channel, message.To, message.Subject, message.Body, null, cancellationToken);
        return new NotificationResult(
            n.Status == NotificationStatus.Sent, n.ProviderMessageId, n.LastError);
    }

    // ── private helpers ─────────────────────────────────────────────────

    private async Task DeliverAsync(
        IApplicationDbContext db,
        BroadcastCampaign campaign,
        NotificationChannel channel,
        string recipientAddress,
        Guid? memberId,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        var notification = Notification.Queue(
            channel, recipientAddress, subject, body, memberId, campaign.Id, _clock.UtcNow);
        db.Notifications.Add(notification);
        await DeliverPersistedAsync(db, notification, memberId, cancellationToken, campaign);
    }

    private async Task DeliverPersistedAsync(
        IApplicationDbContext db,
        Notification notification,
        Guid? memberId,
        CancellationToken cancellationToken,
        BroadcastCampaign? campaign)
    {
        var now = _clock.UtcNow;
        try
        {
            switch (notification.Channel)
            {
                case NotificationChannel.Email:
                {
                    var result = await _email.SendAsync(
                        new EmailMessage(notification.RecipientAddress, notification.Subject,
                                         notification.Body, IsHtml: false),
                        cancellationToken);
                    Apply(notification, result.Delivered, result.ProviderMessageId, result.Error, now);
                    campaign?.RecordResult(result.Delivered);
                    break;
                }
                case NotificationChannel.Sms:
                {
                    var result = await _sms.SendAsync(
                        new SmsMessage(notification.RecipientAddress, notification.Body),
                        cancellationToken);
                    Apply(notification, result.Delivered, result.ProviderMessageId, result.Error, now);
                    campaign?.RecordResult(result.Delivered);
                    break;
                }
                case NotificationChannel.Push:
                {
                    var result = await _push.SendAsync(
                        new PushMessage(notification.RecipientAddress, notification.Subject, notification.Body),
                        cancellationToken);
                    Apply(notification, result.Delivered, result.ProviderMessageId, result.Error, now);
                    campaign?.RecordResult(result.Delivered);

                    if (result.TokenInvalidated && memberId is { } mid)
                    {
                        // Dead FCM token — flip every matching DeviceRegistration
                        // to inactive so subsequent broadcasts skip it.
                        var stale = await db.DeviceRegistrations
                            .Where(d => d.MemberId == mid && d.Token == notification.RecipientAddress)
                            .ToListAsync(cancellationToken);
                        foreach (var d in stale) d.Deactivate(now);
                    }
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Notification delivery threw for {Channel} to {Address}",
                notification.Channel, notification.RecipientAddress);
            notification.MarkFailed(ex.Message, now);
            campaign?.RecordResult(false);
        }
    }

    private static void Apply(
        Notification notification, bool delivered, string? providerMessageId, string? error, DateTime now)
    {
        if (delivered) notification.MarkSent(providerMessageId, now);
        else           notification.MarkFailed(error ?? "Unknown delivery failure.", now);
    }
}
