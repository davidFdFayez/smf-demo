namespace SMF.Application.Common.Interfaces;

/// <summary>
/// Channel-specific abstraction the dispatcher dispatches to. Concrete
/// implementations live in <c>SMF.Infrastructure.Communication</c>
/// (SMTP and SendGrid today). The dev fallback writes to the logger.
/// </summary>
public interface IEmailSender
{
    Task<EmailDeliveryResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

public sealed record EmailMessage(
    string ToAddress,
    string Subject,
    string Body,
    bool IsHtml = false,
    string? ToName = null);

public sealed record EmailDeliveryResult(bool Delivered, string? ProviderMessageId, string? Error);
