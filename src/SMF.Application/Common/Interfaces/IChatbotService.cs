namespace SMF.Application.Common.Interfaces;

/// <summary>
/// Streams an FAQ answer for the user's prompt. The caller (API endpoint)
/// is responsible for forwarding tokens to the client over Server-Sent
/// Events; this interface keeps the AI provider hidden behind a single
/// streaming primitive.
/// </summary>
public interface IChatbotService
{
    /// <summary>Stream an answer one chunk at a time. Implementations are
    /// expected to honor <paramref name="cancellationToken"/> aggressively
    /// so the user can cancel a long-running generation.</summary>
    IAsyncEnumerable<string> StreamReplyAsync(
        IReadOnlyList<ChatTurn> history,
        CancellationToken cancellationToken = default);

    /// <summary>Returns true when the runtime is configured (API key + base
    /// URL); the API exposes this so the floating chat widget can disable
    /// itself in environments without an AI provider.</summary>
    bool IsConfigured { get; }
}

/// <summary>One turn in the chat history forwarded to the model. Mirrors
/// OpenAI's ChatCompletionMessage shape (role + content).</summary>
public sealed record ChatTurn(string Role, string Content);
