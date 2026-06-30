using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMF.Application.Common.Interfaces;

namespace SMF.Infrastructure.Communication.Sms;

/// <summary>
/// Unifonic Messaging API v1 sender. Targets <c>POST /rest/Messages/send</c>
/// which expects an <c>application/x-www-form-urlencoded</c> body — Unifonic
/// rejects JSON for the legacy SendMessage call. Returns the provider's
/// MessageID for traceability.
/// </summary>
internal sealed class UnifonicSmsSender : ISmsSender
{
    public const string HttpClientName = "smf.sms.unifonic";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<SmsOptions> _options;
    private readonly ILogger<UnifonicSmsSender> _logger;

    public UnifonicSmsSender(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<SmsOptions> options,
        ILogger<UnifonicSmsSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<SmsDeliveryResult> SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue.Unifonic;
        if (string.IsNullOrWhiteSpace(opts.AppSid))
            return new SmsDeliveryResult(false, null, "Unifonic AppSid is not configured.");

        var http = _httpClientFactory.CreateClient(HttpClientName);
        var senderId = string.IsNullOrWhiteSpace(opts.SenderId)
            ? _options.CurrentValue.DefaultSenderId
            : opts.SenderId;

        // Unifonic API v1 expects the leading + to be stripped.
        var recipient = message.ToPhoneE164.TrimStart('+');

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["AppSid"]    = opts.AppSid,
            ["SenderID"]  = senderId,
            ["Recipient"] = recipient,
            ["Body"]      = message.Body,
            ["responseType"] = "JSON"
        });

        try
        {
            using var response = await http.PostAsync("/rest/Messages/send", content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Unifonic HTTP {Status} for {Recipient}: {Body}",
                    (int)response.StatusCode, message.ToPhoneE164, body);
                return new SmsDeliveryResult(false, null, $"{(int)response.StatusCode}: {Truncate(body, 300)}");
            }

            // Unifonic returns: { "success":"true","data":{ "MessageID":"...","Status":"... }}
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(body);
                var success = doc.RootElement.TryGetProperty("success", out var s) &&
                              string.Equals(s.GetString(), "true", StringComparison.OrdinalIgnoreCase);
                var providerId = doc.RootElement.TryGetProperty("data", out var data) &&
                                 data.TryGetProperty("MessageID", out var mid)
                    ? mid.ToString()
                    : null;
                if (!success)
                {
                    var err = doc.RootElement.TryGetProperty("errorCode", out var ec) ? ec.ToString() : body;
                    return new SmsDeliveryResult(false, providerId, err);
                }
                return new SmsDeliveryResult(true, providerId, null);
            }
            catch
            {
                // Provider returned non-JSON; surface the raw body for diagnosis.
                return new SmsDeliveryResult(false, null, Truncate(body, 300));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Unifonic delivery failed to {Recipient}", message.ToPhoneE164);
            return new SmsDeliveryResult(false, null, ex.Message);
        }
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n];
}
