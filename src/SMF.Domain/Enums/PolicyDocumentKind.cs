namespace SMF.Domain.Enums;

/// <summary>
/// The three policies every member must accept at signup. Each kind has
/// its own version history, accepted independently, so the audit log can
/// answer "which version did this user agree to and when".
/// </summary>
public enum PolicyDocumentKind
{
    TermsOfService  = 0,
    PrivacyPolicy   = 1,
    CodeOfConduct   = 2
}
