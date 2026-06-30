namespace SMF.Domain.Enums;

/// <summary>
/// Set of channels a single <see cref="Entities.BroadcastCampaign"/> is fanned out
/// across. Stored as a bit mask so a campaign can target any combination of
/// email + SMS + push without joining a separate child table.
/// </summary>
[Flags]
public enum BroadcastChannel
{
    None  = 0,
    Email = 1,
    Sms   = 2,
    Push  = 4,
    All   = Email | Sms | Push
}
