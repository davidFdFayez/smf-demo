using Microsoft.Extensions.Logging;
using SMF.Application.Common.Interfaces;

namespace SMF.Infrastructure.Communication.Push;

internal sealed class LoggingPushSender : IPushSender
{
    private readonly ILogger<LoggingPushSender> _logger;
    public LoggingPushSender(ILogger<LoggingPushSender> logger) => _logger = logger;

    public Task<PushDeliveryResult> SendAsync(PushMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[DEV-PUSH] token={Token} title=\"{Title}\" body=\"{Body}\" data={DataKeys}",
            Truncate(message.DeviceToken, 16), message.Title, message.Body,
            message.Data is null ? "(none)" : string.Join(',', message.Data.Keys));

        return Task.FromResult(new PushDeliveryResult(
            Delivered: true,
            ProviderMessageId: $"dev-push-{Guid.NewGuid():N}",
            Error: null));
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "…";
}
