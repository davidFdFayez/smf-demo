using Microsoft.Extensions.Logging;
using SMF.Application.Common.Interfaces;

namespace SMF.Infrastructure.Communication.Sms;

internal sealed class LoggingSmsSender : ISmsSender
{
    private readonly ILogger<LoggingSmsSender> _logger;
    public LoggingSmsSender(ILogger<LoggingSmsSender> logger) => _logger = logger;

    public Task<SmsDeliveryResult> SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[DEV-SMS] to={To} body=\"{Body}\"", message.ToPhoneE164, message.Body);
        return Task.FromResult(new SmsDeliveryResult(true, $"dev-sms-{Guid.NewGuid():N}", null));
    }
}
