using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMF.Application.Common.Interfaces;

namespace SMF.Infrastructure.Communication.Sms;

/// <summary>
/// Twilio Messages API sender. Hits
/// <c>https://api.twilio.com/2010-04-01/Accounts/{AccountSid}/Messages.json</c>
/// with HTTP Basic auth (AccountSid:AuthToken). The body is form-encoded —
/// Twilio's REST surface predates the JSON fashion.
/// </summary>
internal sealed class TwilioSmsSender : ISmsSender
{
    public const string HttpClientName = "smf.sms.twilio";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<SmsOptions> _options;
    private readonly ILogger<TwilioSmsSender> _logger;

    public TwilioSmsSender(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<SmsOptions> options,
        ILogger<TwilioSmsSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<SmsDeliveryResult> SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue.Twilio;
        if (string.IsNullOrWhiteSpace(opts.AccountSid) || string.IsNullOrWhiteSpace(opts.AuthToken))
            return new SmsDeliveryResult(false, null, "Twilio credentials are not configured.");
        if (string.IsNullOrWhiteSpace(opts.FromPhoneE164))
            return new SmsDeliveryResult(false, null, "Twilio FromPhoneE164 is not configured.");

        var http = _httpClientFactory.CreateClient(HttpClientName);
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{opts.AccountSid}:{opts.AuthToken}"));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["From"] = opts.FromPhoneE164,
            ["To"]   = message.ToPhoneE164,
            ["Body"] = message.Body
        });

        try
        {
            using var response = await http.PostAsync(
                $"/2010-04-01/Accounts/{opts.AccountSid}/Messages.json", content, cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Twilio HTTP {Status} for {Recipient}: {Body}",
                    (int)response.StatusCode, message.ToPhoneE164, body);
                return new SmsDeliveryResult(false, null, $"{(int)response.StatusCode}: {Truncate(body, 300)}");
            }

            string? sid = null;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("sid", out var sidEl)) sid = sidEl.GetString();
            }
            catch { /* shape may have changed; sid stays null */ }

            return new SmsDeliveryResult(true, sid, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Twilio delivery failed to {Recipient}", message.ToPhoneE164);
            return new SmsDeliveryResult(false, null, ex.Message);
        }
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n];
}
