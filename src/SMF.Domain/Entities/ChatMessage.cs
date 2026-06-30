using SMF.Domain.Enums;

namespace SMF.Domain.Entities;

/// <summary>One turn of a <see cref="ChatConversation"/>. Captured per-role
/// (System / User / Assistant) so the OpenAI request envelope can be
/// reconstructed verbatim from history.</summary>
public class ChatMessage
{
    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public ChatMessageRole Role { get; private set; }
    public string Content { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; }

    private ChatMessage() { }

    internal static ChatMessage Create(Guid conversationId, ChatMessageRole role, string content, DateTime nowUtc)
        => new()
        {
            Id             = Guid.NewGuid(),
            ConversationId = conversationId,
            Role           = role,
            Content        = content,
            CreatedAtUtc   = nowUtc
        };
}
