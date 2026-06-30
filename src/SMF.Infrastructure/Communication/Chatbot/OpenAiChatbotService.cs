using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMF.Application.Common.Interfaces;

namespace SMF.Infrastructure.Communication.Chatbot;

/// <summary>
/// Streaming Chat Completions client. Targets <c>POST /v1/chat/completions</c>
/// with <c>stream=true</c> and parses the resulting Server-Sent Events.
/// Compatible with both OpenAI and Azure OpenAI by swapping the base URL +
/// auth header (Azure uses <c>api-key</c>; we keep <c>Authorization</c> here
/// since it works for OpenAI and most Azure deployments accept it on the
/// chat completions URL with the right routing).
/// </summary>
internal sealed class OpenAiChatbotService : IChatbotService
{
    public const string HttpClientName = "smf.chatbot.openai";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<ChatbotOptions> _options;
    private readonly ILogger<OpenAiChatbotService> _logger;

    public OpenAiChatbotService(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<ChatbotOptions> options,
        ILogger<OpenAiChatbotService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.CurrentValue.ApiKey);

    public async IAsyncEnumerable<string> StreamReplyAsync(
        IReadOnlyList<ChatTurn> history,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        if (!IsConfigured)
        {
            yield return "Chatbot is not configured on this environment. Please contact info@smf.sa.";
            yield break;
        }

        var http = _httpClientFactory.CreateClient(HttpClientName);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", opts.ApiKey);

        // Prepend the system prompt unless the caller already supplied one.
        var messages = new List<object>(history.Count + 1);
        if (history.Count == 0 || !string.Equals(history[0].Role, "system", StringComparison.OrdinalIgnoreCase))
            messages.Add(new { role = "system", content = opts.SystemPrompt });

        foreach (var turn in history)
            messages.Add(new { role = turn.Role.ToLowerInvariant(), content = turn.Content });

        var payload = new
        {
            model       = opts.Model,
            temperature = opts.Temperature,
            max_tokens  = opts.MaxTokens,
            stream      = true,
            messages
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload),
                                        Encoding.UTF8, "application/json")
        };

        HttpResponseMessage? response = null;
        string? connectionError = null;
        try
        {
            response = await http.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Chatbot upstream connection failed");
            connectionError = ex.Message;
        }

        if (connectionError is not null)
        {
            yield return "Sorry — the assistant could not be reached. Please try again shortly.";
            yield break;
        }
        if (response is null) yield break;

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Chatbot HTTP {Status}: {Body}", (int)response.StatusCode, body);
                yield return $"The assistant returned an error ({(int)response.StatusCode}). Please try again.";
                yield break;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            // OpenAI streams in SSE format:
            //   data: {...JSON chunk...}
            //   data: [DONE]
            //   <blank line>
            while (!reader.EndOfStream)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null) break;
                if (line.Length == 0) continue;
                if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;

                var data = line[5..].Trim();
                if (data == "[DONE]") yield break;
                if (data.Length == 0) continue;

                string? delta = null;
                try
                {
                    using var doc = JsonDocument.Parse(data);
                    var choices = doc.RootElement.GetProperty("choices");
                    if (choices.GetArrayLength() == 0) continue;

                    var first = choices[0];
                    if (first.TryGetProperty("delta", out var d) &&
                        d.TryGetProperty("content", out var c) &&
                        c.ValueKind == JsonValueKind.String)
                        delta = c.GetString();
                }
                catch (JsonException)
                {
                    // ignore non-JSON keep-alive frames
                    continue;
                }

                if (!string.IsNullOrEmpty(delta))
                    yield return delta!;
            }
        }
    }
}
