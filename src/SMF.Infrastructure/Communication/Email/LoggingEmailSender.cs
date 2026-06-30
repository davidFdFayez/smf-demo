using Microsoft.Extensions.Logging;
using SMF.Application.Common.Interfaces;

namespace SMF.Infrastructure.Communication.Email;

/// <summary>Dev-safe fallback email sender. Logs the rendered envelope and
/// returns a synthetic delivery id without contacting any provider. Used
/// when <c>Email:Provider</c> is <c>Logging</c> or when the configured
/// provider is missing required credentials.</summary>
internal sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => _logger = logger;

    public Task<EmailDeliveryResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[DEV-EMAIL] to={To} ({Name}) subject={Subject} body=\"{Body}\"",
            message.ToAddress, message.ToName ?? "", message.Subject, message.Body);

        return Task.FromResult(new EmailDeliveryResult(
            Delivered: true,
            ProviderMessageId: $"dev-email-{Guid.NewGuid():N}",
            Error: null));
    }
}
