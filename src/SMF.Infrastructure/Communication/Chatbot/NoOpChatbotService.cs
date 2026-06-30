using SMF.Application.Common.Interfaces;

namespace SMF.Infrastructure.Communication.Chatbot;

/// <summary>Used when no AI provider is configured. Lets the API endpoint
/// stay registered without leaking 500s into the floating widget.</summary>
internal sealed class NoOpChatbotService : IChatbotService
{
    public bool IsConfigured => false;

    public async IAsyncEnumerable<string> StreamReplyAsync(
        IReadOnlyList<ChatTurn> history,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield return
            "The Federation assistant is not enabled in this environment. " +
            "For now please email info@smf.sa with your question.";
    }
}
