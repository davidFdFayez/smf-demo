namespace SMF.Domain.Enums;

/// <summary>
/// Possible transitions a <see cref="SMF.Domain.Entities.RoundTimerEvent"/> can represent.
/// Append-only: every action a timekeeper takes becomes a new row so post-match
/// audits can reconstruct the clock independently of the match aggregate's
/// current state.
/// </summary>
public enum TimerAction
{
    RoundStarted = 1,
    Paused = 2,
    Resumed = 3,
    Ended = 4,
    Reset = 5
}
