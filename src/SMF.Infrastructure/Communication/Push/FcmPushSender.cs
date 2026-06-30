using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SMF.Application.Common.Interfaces;

namespace SMF.Infrastructure.Communication.Push;

/// <summary>
/// FCM HTTP v1 sender. Trades a service-account JWT for an OAuth2 token via
/// <see cref="FcmAccessTokenProvider"/>, then POSTs the message envelope to
/// <c>/v1/projects/{projectId}/messages:send</c>.
/// 
/// The HTTP v1 API replaced the legacy server key endpoint (deprecated Jun
/// 2024). It returns a name like <c>projects/X/messages/Y</c> on success;
/// failure surfaces a <c>NOT_REGISTERED</c> error code on stale tokens which
/// the dispatcher uses to deactivate the device row.
/// </summary>
internal sealed class FcmPushSender : IPushSender
{
    public const string HttpClientName = "smf.push.fcm";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FcmAccessTokenProvider _tokens;
    private readonly ILogger<FcmPushSender> _logger;

    public FcmPushSender(
        IHttpClientFactory httpClientFactory,
        FcmAccessTokenProvider tokens,
        ILogger<FcmPushSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _tokens = tokens;
        _logger = logger;
    }

    public async Task<PushDeliveryResult> SendAsync(PushMessage message, CancellationToken cancellationToken = default)
    {
        var token = await _tokens.GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrEmpty(token))
            return new PushDeliveryResult(false, null,
                "FCM is not configured (missing service-account JSON).", false);

        var projectId = _tokens.ProjectId;
        if (string.IsNullOrWhiteSpace(projectId))
            return new PushDeliveryResult(false, null, "FCM ProjectId is not configured.", false);

        var http = _httpClientFactory.CreateClient(HttpClientName);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Convert any non-string data values to strings — FCM requires
        // string-only data members.
        var data = message.Data?
            .ToDictionary(kv => kv.Key, kv => (object)kv.Value);

        var payload = new
        {
            message = new
            {
                token = message.DeviceToken,
                notification = new
                {
                    title = message.Title,
                    body  = message.Body
                },
                data
            }
        };

        try
        {
            using var resp = await http.PostAsJsonAsync(
                $"/v1/projects/{projectId}/messages:send", payload, cancellationToken);

            var body = await resp.Content.ReadAsStringAsync(cancellationToken);
            if (resp.IsSuccessStatusCode)
            {
                string? messageId = null;
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("name", out var name))
                        messageId = name.GetString();
                }
                catch { /* tolerate shape changes */ }

                return new PushDeliveryResult(true, messageId, null, false);
            }

            // Inspect FCM error shape to flag invalid/expired tokens.
            var invalidated = false;
            string error = body;
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error", out var err))
                {
                    var status = err.TryGetProperty("status", out var st) ? st.GetString() : null;
                    if (string.Equals(status, "NOT_FOUND", StringComparison.Ordinal) ||
                        string.Equals(status, "UNREGISTERED", StringComparison.Ordinal) ||
                        string.Equals(status, "INVALID_ARGUMENT", StringComparison.Ordinal))
                        invalidated = true;
                    if (err.TryGetProperty("message", out var msg) && msg.GetString() is { } m)
                        error = m;
                }
            }
            catch { /* keep raw body */ }

            _logger.LogWarning("FCM HTTP {Status}: {Body}", (int)resp.StatusCode, body);
            return new PushDeliveryResult(false, null, error, invalidated);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "FCM call failed");
            return new PushDeliveryResult(false, null, ex.Message, false);
        }
    }
}
