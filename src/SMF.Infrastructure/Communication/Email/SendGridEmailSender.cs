using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMF.Application.Common.Interfaces;

namespace SMF.Infrastructure.Communication.Email;

/// <summary>
/// SendGrid transport over the v3 REST API. We hit the JSON endpoint
/// directly (no SDK) — the request shape is small, the SDK is heavyweight,
/// and we already use this raw-HTTP pattern for the Tap payment gateway.
/// </summary>
internal sealed class SendGridEmailSender : IEmailSender
{
    public const string HttpClientName = "smf.sendgrid";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<EmailOptions> _options;
    private readonly ILogger<SendGridEmailSender> _logger;

    public SendGridEmailSender(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<EmailOptions> options,
        ILogger<SendGridEmailSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<EmailDeliveryResult> SendAsync(
        EmailMessage message, CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        var sg = opts.SendGrid;

        if (string.IsNullOrWhiteSpace(sg.ApiKey))
            return new EmailDeliveryResult(false, null, "SendGrid ApiKey is not configured.");

        var http = _httpClientFactory.CreateClient(HttpClientName);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sg.ApiKey);

        var contentType = message.IsHtml ? "text/html" : "text/plain";
        var payload = new
        {
            personalizations = new[]
            {
                new
                {
                    to = new[]
                    {
                        new { email = message.ToAddress, name = message.ToName ?? "" }
                    }
                }
            },
            from    = new { email = opts.FromAddress, name = opts.FromName },
            subject = message.Subject,
            content = new[]
            {
                new { type = contentType, value = message.Body }
            }
        };

        try
        {
            using var response = await http.PostAsJsonAsync(
                "/v3/mail/send", payload, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                // SendGrid returns 202 + an X-Message-Id header for accepted requests.
                var providerId = response.Headers.TryGetValues("X-Message-Id", out var ids)
                    ? ids.FirstOrDefault()
                    : null;
                return new EmailDeliveryResult(true, providerId, null);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "SendGrid rejected delivery to {Recipient}: {Status} {Body}",
                message.ToAddress, (int)response.StatusCode, body);
            return new EmailDeliveryResult(false, null, $"{(int)response.StatusCode}: {Truncate(body, 400)}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "SendGrid call failed to {Recipient}", message.ToAddress);
            return new EmailDeliveryResult(false, null, ex.Message);
        }
    }

    private static string Truncate(string s, int n)
        => s.Length <= n ? s : s[..n];
}
