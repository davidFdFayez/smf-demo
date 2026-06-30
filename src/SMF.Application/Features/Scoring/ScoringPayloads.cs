using SMF.Domain.Enums;

namespace SMF.Application.Features.Scoring;

/// <summary>
/// Broadcast to every subscriber in a match group each time a side referee
/// registers a strike. Kept intentionally small to minimise wire size under
/// the MessagePack protocol.
/// </summary>
public sealed record StrikeUpdatePayload(
    string EventId,
    string MatchId,
    string RefereeId,
    FighterColor FighterColor,
    DateTimeOffset OccurredAtUtc);

/// <summary>
/// A complete scoreboard override issued by the head referee.
/// <paramref name="Round"/> is optional — when null the override applies to the match total.
/// </summary>
public sealed record ScoreOverride(
    int Red,
    int Blue,
    int? Round = null);

public sealed record ScoreOverridePayload(
    string EventId,
    string MatchId,
    string HeadRefereeId,
    ScoreOverride NewScore,
    DateTimeOffset OccurredAtUtc);

// ── Timer / Round clock (PDF Phase 3 → Timekeeper) ────────────────────────

/// <summary>Snapshot of the match clock at a point in time. Broadcast to every
/// match subscriber (scoreboard, overlay, referees) on every round transition.</summary>
public sealed record TimerStatePayload(
    string MatchId,
    int CurrentRound,
    int RoundDurationSeconds,
    int ElapsedSeconds,
    bool IsRunning,
    TimerActionKind LastAction,
    DateTimeOffset OccurredAtUtc);

public enum TimerActionKind
{
    RoundStarted = 1,
    Paused = 2,
    Resumed = 3,
    Ended = 4,
    Reset = 5,
    Tick = 6
}
