namespace SMF.Domain.Enums;

/// <summary>OpenAI / Azure OpenAI chat role enum used to persist
/// <see cref="Entities.ChatMessage"/> turns.</summary>
public enum ChatMessageRole
{
    System    = 0,
    User      = 1,
    Assistant = 2
}
