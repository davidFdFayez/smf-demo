using SMF.Domain.Enums;

namespace SMF.Domain.Entities;

/// <summary>
/// Aggregate root representing a scheduled MuayThai match.
/// Owns its referee assignment list; the head referee is always
/// also an assigned referee.
/// </summary>
public class Match
{
    public Guid Id { get; private set; }

    /// <summary>Human-friendly business key used over the wire (e.g. "match-001").</summary>
    public string Code { get; private set; } = default!;

    public Guid HeadRefereeId { get; private set; }

    public MatchStatus Status { get; private set; }

    public DateTime ScheduledAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    // Live streaming (PDF §9). Optional per-match broadcast URL — overrides
    // the parent event's stream when set so individual bouts can have their
    // own feed (e.g. split-channel pay-per-view).
    public LiveStreamProvider? LiveStreamProvider { get; private set; }
    public string? LiveStreamUrl { get; private set; }

    // ── Timekeeper / round clock (Phase 3) ───────────────────────────────────
    // The timekeeper owns the match clock. We store the minimal state needed
    // to reconstruct "what time is it now" without a background worker:
    //   * `RoundStartedAtUtc` is when the currently running round last
    //      transitioned from Paused/Stopped → Running.
    //   * `RoundElapsedSecondsAtStart` is how many seconds had already elapsed
    //      when the round last resumed, so `now − start + elapsedAtStart`
    //      gives the authoritative elapsed seconds.
    //   * `IsTimerRunning` lets consumers freeze/unfreeze display without
    //      recomputing timestamps locally.
    public Guid? TimekeeperId { get; private set; }
    public int CurrentRound { get; private set; }
    public int RoundDurationSeconds { get; private set; }
    public DateTime? RoundStartedAtUtc { get; private set; }
    public int RoundElapsedSecondsAtStart { get; private set; }
    public bool IsTimerRunning { get; private set; }

    private readonly List<MatchReferee> _referees = new();
    public IReadOnlyCollection<MatchReferee> Referees => _referees.AsReadOnly();

    private Match() { }

    public static Match Schedule(
        string code,
        Guid headRefereeId,
        DateTime scheduledAtUtc,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Match code is required.", nameof(code));
        if (headRefereeId == Guid.Empty)
            throw new ArgumentException("Head referee id is required.", nameof(headRefereeId));

        var match = new Match
        {
            Id = Guid.NewGuid(),
            Code = code.Trim(),
            HeadRefereeId = headRefereeId,
            ScheduledAtUtc = scheduledAtUtc,
            Status = MatchStatus.Scheduled,
            CreatedAtUtc = nowUtc
        };

        // The head referee is always an assigned referee.
        match._referees.Add(new MatchReferee(headRefereeId));

        return match;
    }

    public void AssignReferee(Guid refereeId)
    {
        if (refereeId == Guid.Empty)
            throw new ArgumentException("Referee id is required.", nameof(refereeId));

        if (_referees.Any(r => r.RefereeId == refereeId))
            return;

        _referees.Add(new MatchReferee(refereeId));
    }

    public bool IsAssignedReferee(Guid refereeId)
        => _referees.Any(r => r.RefereeId == refereeId);

    public bool IsHeadReferee(Guid refereeId)
        => HeadRefereeId == refereeId;

    /// <summary>Attach a live-stream URL + provider. Pass <c>null</c>s to clear.</summary>
    public void SetLiveStream(LiveStreamProvider? provider, string? url)
    {
        if (provider is null || string.IsNullOrWhiteSpace(url))
        {
            LiveStreamProvider = null;
            LiveStreamUrl = null;
            return;
        }
        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            throw new ArgumentException("Live stream URL must be absolute.", nameof(url));
        LiveStreamProvider = provider;
        LiveStreamUrl = url.Trim();
    }

    /// <summary>Assign the officiating timekeeper for this bout.</summary>
    public void AssignTimekeeper(Guid timekeeperId)
    {
        if (timekeeperId == Guid.Empty)
            throw new ArgumentException("Timekeeper id is required.", nameof(timekeeperId));
        TimekeeperId = timekeeperId;
    }

    public bool IsTimekeeper(Guid userId)
        => TimekeeperId.HasValue && TimekeeperId.Value == userId;

    /// <summary>
    /// Start (or restart) a round with a fresh clock at 0 seconds. Used by the
    /// timekeeper both for round 1 and for each subsequent round.
    /// </summary>
    public void StartRound(int roundNumber, int durationSeconds, DateTime nowUtc)
    {
        if (roundNumber < 1) throw new ArgumentOutOfRangeException(nameof(roundNumber));
        if (durationSeconds < 1) throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        if (Status == MatchStatus.Completed)
            throw new InvalidOperationException("Cannot start a round on a completed match.");

        CurrentRound = roundNumber;
        RoundDurationSeconds = durationSeconds;
        RoundStartedAtUtc = nowUtc;
        RoundElapsedSecondsAtStart = 0;
        IsTimerRunning = true;
    }

    /// <summary>Pause the currently running round. Safe to call when already paused.</summary>
    public int PauseRound(DateTime nowUtc)
    {
        if (!IsTimerRunning) return RoundElapsedSecondsAtStart;
        var elapsed = CurrentElapsedSeconds(nowUtc);
        IsTimerRunning = false;
        RoundElapsedSecondsAtStart = elapsed;
        RoundStartedAtUtc = null;
        return elapsed;
    }

    /// <summary>Resume a paused round. Does nothing if the clock is already running.</summary>
    public void ResumeRound(DateTime nowUtc)
    {
        if (IsTimerRunning) return;
        if (CurrentRound < 1)
            throw new InvalidOperationException("No round has been started yet.");
        RoundStartedAtUtc = nowUtc;
        IsTimerRunning = true;
    }

    /// <summary>Stop the clock and record the elapsed seconds at end.</summary>
    public int EndRound(DateTime nowUtc)
    {
        var elapsed = IsTimerRunning
            ? CurrentElapsedSeconds(nowUtc)
            : RoundElapsedSecondsAtStart;
        IsTimerRunning = false;
        RoundStartedAtUtc = null;
        RoundElapsedSecondsAtStart = elapsed;
        return elapsed;
    }

    public int CurrentElapsedSeconds(DateTime nowUtc)
    {
        if (!IsTimerRunning || RoundStartedAtUtc is null)
            return RoundElapsedSecondsAtStart;
        var delta = (int)Math.Max(0, (nowUtc - RoundStartedAtUtc.Value).TotalSeconds);
        var total = RoundElapsedSecondsAtStart + delta;
        return Math.Min(total, RoundDurationSeconds);
    }
}
