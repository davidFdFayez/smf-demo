using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SMF.Application.Common.Interfaces;
using SMF.Application.Features.Scoring;
using SMF.Domain.Entities;
using SMF.Domain.Enums;

namespace SMF.Api.Hubs;

/// <summary>
/// Real-time scoring hub for MuayThai matches.
/// <para>
/// Subscribers (Head Referee Dashboards, Public Scoreboards) join a per-match
/// group via <see cref="JoinMatch"/>. Side referees stream strikes through
/// <see cref="SubmitStrike"/>; the head referee issues scoreboard corrections
/// through <see cref="OverrideScore"/>.
/// </para>
/// <para>
/// The hub requires an authenticated connection. The referee id supplied
/// in each method call is cross-checked against <c>Context.UserIdentifier</c>
/// so a caller cannot spoof another referee.
/// </para>
/// <para>
/// Every strike / override is appended to the <see cref="IScoringEventStore"/>
/// <em>before</em> fan-out so a scoreboard reconnecting mid-match can replay
/// the full state.
/// </para>
/// </summary>
[Authorize]
public sealed class MatchScoringHub : Hub<IMatchScoringClient>
{
    public const string Path = "/hubs/match-scoring";

    private readonly IMatchAuthorizationService _auth;
    private readonly IScoringEventStore _events;
    private readonly IMatchRepository _matches;
    private readonly ILogger<MatchScoringHub> _logger;

    public MatchScoringHub(
        IMatchAuthorizationService auth,
        IScoringEventStore events,
        IMatchRepository matches,
        ILogger<MatchScoringHub> logger)
    {
        _auth = auth;
        _events = events;
        _matches = matches;
        _logger = logger;
    }

    /// <summary>Subscribe the caller to a match's broadcast group.</summary>
    public Task JoinMatch(string matchId)
    {
        if (string.IsNullOrWhiteSpace(matchId))
            throw new HubException("matchId is required.");

        return Groups.AddToGroupAsync(Context.ConnectionId, matchId, Context.ConnectionAborted);
    }

    /// <summary>Unsubscribe the caller from a match's broadcast group.</summary>
    public Task LeaveMatch(string matchId)
    {
        if (string.IsNullOrWhiteSpace(matchId))
            throw new HubException("matchId is required.");

        return Groups.RemoveFromGroupAsync(Context.ConnectionId, matchId, Context.ConnectionAborted);
    }

    /// <summary>
    /// Register a strike scored by a side referee. Persists an append-only
    /// <see cref="StrikeEvent"/>, then broadcasts a <see cref="StrikeUpdatePayload"/>
    /// to every client in the match group.
    /// </summary>
    public async Task SubmitStrike(string matchId, string refereeId, string fighterColor)
    {
        if (string.IsNullOrWhiteSpace(matchId))
            throw new HubException("matchId is required.");
        if (string.IsNullOrWhiteSpace(refereeId))
            throw new HubException("refereeId is required.");

        if (!Enum.TryParse<FighterColor>(fighterColor, ignoreCase: true, out var color)
            || !Enum.IsDefined(color))
        {
            throw new HubException(
                $"Invalid fighter color '{fighterColor}'. Expected 'Red' or 'Blue'.");
        }

        var refereeGuid = ParseAndVerifyCallerIdentity(refereeId);

        var access = await _auth.GetAccessAsync(matchId, refereeGuid, Context.ConnectionAborted);
        if (access is null)
            throw new HubException($"Match '{matchId}' was not found.");
        if (!access.IsAssigned)
        {
            _logger.LogWarning(
                "Rejected strike: referee {RefereeId} is not assigned to match {MatchCode}",
                refereeGuid, matchId);
            throw new HubException("Referee is not assigned to this match.");
        }

        var occurredAt = DateTime.UtcNow;
        var strikeEvent = StrikeEvent.Record(access.MatchId, refereeGuid, color, occurredAt);

        // Durability first, fan-out second — guarantees late joiners can replay.
        await _events.RecordStrikeAsync(strikeEvent, Context.ConnectionAborted);

        var payload = new StrikeUpdatePayload(
            EventId: strikeEvent.Id.ToString("N"),
            MatchId: access.MatchCode,
            RefereeId: refereeGuid.ToString(),
            FighterColor: color,
            OccurredAtUtc: new DateTimeOffset(occurredAt, TimeSpan.Zero));

        await Clients
            .Group(matchId)
            .ReceiveStrikeUpdate(payload);

        _logger.LogInformation(
            "Strike broadcast: match={MatchCode} referee={RefereeId} color={Color} event={EventId}",
            matchId, refereeGuid, color, payload.EventId);
    }

    /// <summary>
    /// Override the match scoreboard. Only the designated head referee for
    /// the match may invoke this; other callers get a <see cref="HubException"/>.
    /// </summary>
    public async Task OverrideScore(string matchId, string headRefereeId, ScoreOverride newScore)
    {
        if (string.IsNullOrWhiteSpace(matchId))
            throw new HubException("matchId is required.");
        if (string.IsNullOrWhiteSpace(headRefereeId))
            throw new HubException("headRefereeId is required.");
        if (newScore is null)
            throw new HubException("newScore is required.");
        if (newScore.Red < 0 || newScore.Blue < 0)
            throw new HubException("Scores must be non-negative.");
        if (newScore.Round is { } round && round < 1)
            throw new HubException("Round must be 1 or greater when specified.");

        var headGuid = ParseAndVerifyCallerIdentity(headRefereeId);

        var access = await _auth.GetAccessAsync(matchId, headGuid, Context.ConnectionAborted);
        if (access is null)
            throw new HubException($"Match '{matchId}' was not found.");
        if (!access.IsHeadReferee)
        {
            _logger.LogWarning(
                "Rejected override: {RefereeId} is not the head referee of match {MatchCode}",
                headGuid, matchId);
            throw new HubException("Only the head referee may override the score for this match.");
        }

        var occurredAt = DateTime.UtcNow;
        var overrideEvent = ScoreOverrideEvent.Record(
            access.MatchId, headGuid, newScore.Red, newScore.Blue, newScore.Round, occurredAt);

        await _events.RecordOverrideAsync(overrideEvent, Context.ConnectionAborted);

        var payload = new ScoreOverridePayload(
            EventId: overrideEvent.Id.ToString("N"),
            MatchId: access.MatchCode,
            HeadRefereeId: headGuid.ToString(),
            NewScore: newScore,
            OccurredAtUtc: new DateTimeOffset(occurredAt, TimeSpan.Zero));

        await Clients
            .Group(matchId)
            .ReceiveScoreOverride(payload);

        _logger.LogInformation(
            "Score override broadcast: match={MatchCode} head={Head} red={Red} blue={Blue} round={Round} event={EventId}",
            matchId, headGuid, newScore.Red, newScore.Blue, newScore.Round, payload.EventId);
    }

    /// <summary>
    /// Timekeeper-only: start the currently-selected round at 0 seconds.
    /// Broadcasts <see cref="IMatchScoringClient.ReceiveTimerUpdate"/>.
    /// </summary>
    public async Task StartRound(string matchId, string timekeeperId, int roundNumber, int durationSeconds)
    {
        var (match, _) = await AuthorizeTimekeeperAsync(matchId, timekeeperId);

        if (roundNumber < 1) throw new HubException("roundNumber must be >= 1.");
        if (durationSeconds < 1) throw new HubException("durationSeconds must be >= 1.");

        var now = DateTime.UtcNow;
        match.StartRound(roundNumber, durationSeconds, now);
        await _matches.SaveChangesAsync(Context.ConnectionAborted);

        await RecordAndBroadcastTimerAsync(match, TimerAction.RoundStarted, TimerActionKind.RoundStarted, now);
    }

    public async Task PauseRound(string matchId, string timekeeperId)
    {
        var (match, _) = await AuthorizeTimekeeperAsync(matchId, timekeeperId);

        var now = DateTime.UtcNow;
        match.PauseRound(now);
        await _matches.SaveChangesAsync(Context.ConnectionAborted);

        await RecordAndBroadcastTimerAsync(match, TimerAction.Paused, TimerActionKind.Paused, now);
    }

    public async Task ResumeRound(string matchId, string timekeeperId)
    {
        var (match, _) = await AuthorizeTimekeeperAsync(matchId, timekeeperId);

        var now = DateTime.UtcNow;
        match.ResumeRound(now);
        await _matches.SaveChangesAsync(Context.ConnectionAborted);

        await RecordAndBroadcastTimerAsync(match, TimerAction.Resumed, TimerActionKind.Resumed, now);
    }

    public async Task EndRound(string matchId, string timekeeperId)
    {
        var (match, _) = await AuthorizeTimekeeperAsync(matchId, timekeeperId);

        var now = DateTime.UtcNow;
        match.EndRound(now);
        await _matches.SaveChangesAsync(Context.ConnectionAborted);

        await RecordAndBroadcastTimerAsync(match, TimerAction.Ended, TimerActionKind.Ended, now);
    }

    /// <summary>Let any subscriber pull the authoritative clock on-demand.</summary>
    public async Task RequestTimerState(string matchId)
    {
        if (string.IsNullOrWhiteSpace(matchId))
            throw new HubException("matchId is required.");

        var match = await _matches.GetByCodeAsync(matchId, Context.ConnectionAborted);
        if (match is null) throw new HubException($"Match '{matchId}' was not found.");

        var now = DateTime.UtcNow;
        var payload = new TimerStatePayload(
            MatchId: match.Code,
            CurrentRound: Math.Max(1, match.CurrentRound),
            RoundDurationSeconds: Math.Max(0, match.RoundDurationSeconds),
            ElapsedSeconds: match.CurrentElapsedSeconds(now),
            IsRunning: match.IsTimerRunning,
            LastAction: TimerActionKind.Tick,
            OccurredAtUtc: new DateTimeOffset(now, TimeSpan.Zero));

        await Clients.Caller.ReceiveTimerUpdate(payload);
    }

    private async Task<(Match match, MatchAccess access)> AuthorizeTimekeeperAsync(
        string matchId, string timekeeperId)
    {
        if (string.IsNullOrWhiteSpace(matchId))
            throw new HubException("matchId is required.");
        if (string.IsNullOrWhiteSpace(timekeeperId))
            throw new HubException("timekeeperId is required.");

        var callerGuid = ParseAndVerifyCallerIdentity(timekeeperId);
        var access = await _auth.GetAccessAsync(matchId, callerGuid, Context.ConnectionAborted);
        if (access is null)
            throw new HubException($"Match '{matchId}' was not found.");

        // Timekeeper OR head referee may drive the clock — lets the head take
        // over if the appointed timekeeper drops out mid-match.
        if (!access.IsTimekeeper && !access.IsHeadReferee)
        {
            _logger.LogWarning(
                "Rejected timer action: {UserId} is not the timekeeper for match {MatchCode}",
                callerGuid, matchId);
            throw new HubException("Only the timekeeper or head referee may control the clock.");
        }

        var match = await _matches.GetByIdAsync(access.MatchId, Context.ConnectionAborted)
            ?? throw new HubException($"Match '{matchId}' was not found.");
        return (match, access);
    }

    private async Task RecordAndBroadcastTimerAsync(
        Match match, TimerAction action, TimerActionKind kind, DateTime occurredAt)
    {
        var elapsed = match.CurrentElapsedSeconds(occurredAt);

        var timerEvent = RoundTimerEvent.Record(
            match.Id,
            match.TimekeeperId ?? match.HeadRefereeId,
            action,
            Math.Max(1, match.CurrentRound),
            Math.Max(1, match.RoundDurationSeconds),
            elapsed,
            occurredAt);

        await _events.RecordTimerAsync(timerEvent, Context.ConnectionAborted);

        var payload = new TimerStatePayload(
            MatchId: match.Code,
            CurrentRound: match.CurrentRound,
            RoundDurationSeconds: match.RoundDurationSeconds,
            ElapsedSeconds: elapsed,
            IsRunning: match.IsTimerRunning,
            LastAction: kind,
            OccurredAtUtc: new DateTimeOffset(occurredAt, TimeSpan.Zero));

        await Clients.Group(match.Code).ReceiveTimerUpdate(payload);
    }

    /// <summary>
    /// Validates the supplied referee id is a GUID and matches the authenticated
    /// caller's identity — prevents a connected referee from impersonating another.
    /// </summary>
    private Guid ParseAndVerifyCallerIdentity(string refereeId)
    {
        if (!Guid.TryParse(refereeId, out var refereeGuid))
            throw new HubException("refereeId must be a valid GUID.");

        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var callerGuid))
            throw new HubException("Authenticated caller does not have a valid identity.");

        if (callerGuid != refereeGuid)
            throw new HubException("Caller identity does not match the supplied referee id.");

        return refereeGuid;
    }
}
