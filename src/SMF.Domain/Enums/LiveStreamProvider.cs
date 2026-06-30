namespace SMF.Domain.Enums;

/// <summary>
/// Supported live-streaming providers for federation broadcasts. We keep this
/// as a whitelist (not free text) so the frontend can pick the right embed
/// renderer and we never end up pointing iframes at arbitrary user-supplied
/// origins. Matches PDF §9 "Advanced Features → Live streaming integration".
/// </summary>
public enum LiveStreamProvider
{
    YouTube = 1,
    Twitch = 2,
    Vimeo = 3,
    CustomEmbed = 4
}
