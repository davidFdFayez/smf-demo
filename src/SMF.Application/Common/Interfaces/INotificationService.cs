using SMF.Domain.Enums;

namespace SMF.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the delivery gateway. A logging implementation ships in
/// dev; production swaps in an SMTP / SMS-gateway provider via DI. Kept
/// channel-agnostic so the same abstraction covers email, SMS and push.
/// </summary>
public interface INotificationService
{
    Task<NotificationResult> SendAsync(
        NotificationMessage message,
        CancellationToken cancellationToken = default);
}

public sealed record NotificationMessage(
    NotificationChannel Channel,
    string To,
    string Subject,
    string Body);

public sealed record NotificationResult(bool Delivered, string? ProviderMessageId, string? Error);
