using SMF.Domain.Enums;

namespace SMF.Domain.Entities;

/// <summary>
/// A single user's chat thread with the FAQ assistant. Persisted so admins
/// can audit usage, train better responses, and so a returning user can
/// resume their conversation. Either <see cref="MemberId"/> or
/// <see cref="GuestKey"/> is set; never both.
/// </summary>
public class ChatConversation
{
    public Guid Id { get; private set; }
    public Guid? MemberId { get; private set; }

    /// <summary>UUID assigned by the public site (stored in localStorage) to
    /// keep guest sessions stable across page reloads.</summary>
    public Guid? GuestKey { get; private set; }

    public string Title { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private readonly List<ChatMessage> _messages = new();
    public IReadOnlyCollection<ChatMessage> Messages => _messages.AsReadOnly();

    private ChatConversation() { }

    public static ChatConversation Start(Guid? memberId, Guid? guestKey, string title, DateTime nowUtc)
    {
        var memberSet = memberId is { } m && m != Guid.Empty;
        var guestSet  = guestKey is { } g && g != Guid.Empty;
        if (memberSet == guestSet)
            throw new ArgumentException(
                "Exactly one of MemberId or GuestKey must be supplied.");

        return new ChatConversation
        {
            Id           = Guid.NewGuid(),
            MemberId     = memberSet ? memberId : null,
            GuestKey     = guestSet  ? guestKey : null,
            Title        = string.IsNullOrWhiteSpace(title) ? "Federation FAQ chat" : title.Trim(),
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    public ChatMessage AddMessage(ChatMessageRole role, string content, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Message content required.", nameof(content));

        var message = ChatMessage.Create(Id, role, content, nowUtc);
        _messages.Add(message);
        UpdatedAtUtc = nowUtc;
        return message;
    }
}
