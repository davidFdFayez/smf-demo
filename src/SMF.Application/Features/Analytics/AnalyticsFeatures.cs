using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Entities;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Analytics;

// PDF §9 "Advanced Features → AI analytics"
// Pragmatic MVP: the "AI" is a suite of server-side heuristics derived from
// existing scoring + tournament data (no external ML dependency). Every
// metric ships with a human-readable narrative so the UI can render an
// insights panel without owning the math.

// ─── DTOs ───────────────────────────────────────────────────────────────────

public sealed record StrikeBucket(int MinuteOffset, int RedStrikes, int BlueStrikes);

public sealed record FighterInsight(
    string Label,
    int StrikeCount,
    double StrikeShare,
    int LongestStreak,
    double StrikesPerMinute);

public sealed record MatchInsights(
    string MatchCode,
    MatchStatus Status,
    DateTime ScheduledAtUtc,
    int TotalStrikes,
    double MatchDurationMinutes,
    FighterInsight Red,
    FighterInsight Blue,
    IReadOnlyList<StrikeBucket> Timeline,
    string MomentumLabel,
    string PredictionNarrative,
    double RedWinProbability);

public sealed record AthleteForm(
    Guid MemberId,
    string FullName,
    string SmfId,
    int TotalMatches,
    int Wins,
    int Losses,
    double WinRate,
    int RecentGoldMedals,
    int RecentSilverMedals,
    int RecentBronzeMedals,
    string TrendLabel,
    string NextMatchOutlook);

public sealed record FederationPulse(
    int TotalMembers,
    int ApprovedMembers,
    int ActiveClubs,
    int UpcomingEvents,
    int PublishedCourses,
    int ActiveEnrollments,
    int MatchesLast30Days,
    int StrikesLast30Days,
    double AverageStrikesPerMatch,
    IReadOnlyList<string> Highlights);

// ─── Match insights ─────────────────────────────────────────────────────────

public sealed record GetMatchInsightsQuery(string MatchCode) : IRequest<MatchInsights>;

public sealed class GetMatchInsightsQueryHandler
    : IRequestHandler<GetMatchInsightsQuery, MatchInsights>
{
    private readonly IApplicationDbContext _db;
    private readonly IScoringEventStore _events;

    public GetMatchInsightsQueryHandler(IApplicationDbContext db, IScoringEventStore events)
    {
        _db = db;
        _events = events;
    }

    public async Task<MatchInsights> Handle(GetMatchInsightsQuery request, CancellationToken ct)
    {
        var match = await _db.Matches.AsNoTracking()
                        .FirstOrDefaultAsync(m => m.Code == request.MatchCode, ct)
                    ?? throw new NotFoundException(nameof(Match), request.MatchCode);

        var strikes = await _events.GetStrikesAsync(match.Id, ct);
        if (strikes.Count == 0)
        {
            return new MatchInsights(
                match.Code, match.Status, match.ScheduledAtUtc,
                0, 0,
                ZeroFighter("Red"), ZeroFighter("Blue"),
                Array.Empty<StrikeBucket>(),
                "No scoring activity yet",
                "Insufficient data to predict a winner.",
                0.5);
        }

        var first = strikes.Min(s => s.OccurredAtUtc);
        var last = strikes.Max(s => s.OccurredAtUtc);
        var minutes = Math.Max(1, (last - first).TotalMinutes);

        var red = BuildFighter("Red", strikes, FighterColor.Red, minutes);
        var blue = BuildFighter("Blue", strikes, FighterColor.Blue, minutes);

        // Cap timeline buckets so a match with strikes spread over months does
        // not return a multi-megabyte payload (which times out the watch page).
        const int maxTimelineMinutes = 90;
        var timelineStart = minutes > maxTimelineMinutes
            ? last.AddMinutes(-maxTimelineMinutes)
            : first;
        var timelineSpanMinutes = minutes > maxTimelineMinutes
            ? maxTimelineMinutes
            : minutes;
        var totalBuckets = (int)Math.Ceiling(timelineSpanMinutes);

        var buckets = new List<StrikeBucket>();
        for (var m = 0; m <= totalBuckets; m++)
        {
            var bucketStart = timelineStart.AddMinutes(m);
            var bucketEnd = timelineStart.AddMinutes(m + 1);
            var r = strikes.Count(s => s.FighterColor == FighterColor.Red
                                        && s.OccurredAtUtc >= bucketStart
                                        && s.OccurredAtUtc < bucketEnd);
            var b = strikes.Count(s => s.FighterColor == FighterColor.Blue
                                        && s.OccurredAtUtc >= bucketStart
                                        && s.OccurredAtUtc < bucketEnd);
            if (r + b == 0 && m > 0 && m == totalBuckets) continue;
            buckets.Add(new StrikeBucket(m, r, b));
        }

        var momentum = DescribeMomentum(buckets);
        var prob = NaiveWinProbability(red.StrikeCount, blue.StrikeCount,
                                       red.LongestStreak, blue.LongestStreak);
        var narrative = DescribePrediction(prob, red, blue);

        return new MatchInsights(
            match.Code, match.Status, match.ScheduledAtUtc,
            strikes.Count, Math.Round(minutes, 1),
            red, blue, buckets, momentum, narrative, prob);
    }

    private static FighterInsight ZeroFighter(string label) =>
        new(label, 0, 0, 0, 0);

    private static FighterInsight BuildFighter(
        string label, IReadOnlyList<StrikeEvent> strikes, FighterColor color, double minutes)
    {
        var mine = strikes.Where(s => s.FighterColor == color).ToList();
        var total = strikes.Count;
        var share = total == 0 ? 0 : Math.Round((double)mine.Count / total, 3);
        var streak = LongestStreak(strikes, color);
        var perMinute = Math.Round(mine.Count / minutes, 2);
        return new FighterInsight(label, mine.Count, share, streak, perMinute);
    }

    private static int LongestStreak(IReadOnlyList<StrikeEvent> strikes, FighterColor color)
    {
        var max = 0; var cur = 0;
        foreach (var s in strikes.OrderBy(s => s.OccurredAtUtc))
        {
            if (s.FighterColor == color) { cur++; if (cur > max) max = cur; }
            else cur = 0;
        }
        return max;
    }

    private static string DescribeMomentum(IReadOnlyList<StrikeBucket> buckets)
    {
        if (buckets.Count == 0) return "No scoring activity yet";
        var last = buckets.TakeLast(Math.Min(3, buckets.Count)).ToList();
        var r = last.Sum(b => b.RedStrikes);
        var b = last.Sum(b => b.BlueStrikes);
        if (r == b) return "Evenly matched over the closing minutes";
        return r > b
            ? $"Red is pushing tempo (+{r - b} strikes in the last {last.Count} min)"
            : $"Blue is pushing tempo (+{b - r} strikes in the last {last.Count} min)";
    }

    private static double NaiveWinProbability(int red, int blue, int redStreak, int blueStreak)
    {
        // Elementary logistic over strike share weighted by longest streak —
        // enough to be directionally informative without pretending to be ML.
        var diff = (red - blue) + 0.4 * (redStreak - blueStreak);
        var prob = 1.0 / (1.0 + Math.Exp(-diff / 5.0));
        return Math.Round(Math.Clamp(prob, 0.05, 0.95), 3);
    }

    private static string DescribePrediction(double prob, FighterInsight red, FighterInsight blue)
    {
        if (prob >= 0.66)
            return $"Red favoured — {red.StrikeCount} vs {blue.StrikeCount} strikes, " +
                   $"{red.StrikesPerMinute:F1} strikes/min tempo.";
        if (prob <= 0.34)
            return $"Blue favoured — {blue.StrikeCount} vs {red.StrikeCount} strikes, " +
                   $"{blue.StrikesPerMinute:F1} strikes/min tempo.";
        return "Too close to call — scoring is within one clean exchange of flipping.";
    }
}

// ─── Athlete form ───────────────────────────────────────────────────────────

public sealed record GetAthleteFormQuery(Guid MemberId) : IRequest<AthleteForm>;

public sealed class GetAthleteFormQueryHandler
    : IRequestHandler<GetAthleteFormQuery, AthleteForm>
{
    private readonly IApplicationDbContext _db;
    public GetAthleteFormQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<AthleteForm> Handle(GetAthleteFormQuery request, CancellationToken ct)
    {
        var member = await _db.Members.AsNoTracking()
                         .FirstOrDefaultAsync(m => m.Id == request.MemberId, ct)
                     ?? throw new NotFoundException(nameof(Member), request.MemberId);

        // Pull all completed tournaments + their bracket matches once and
        // compute everything in memory (same shape as RankingsFeatures, which
        // is sized for federation-scale data — thousands of tournaments, not
        // millions).
        var tournaments = await _db.Tournaments.AsNoTracking()
            .Include(t => t.Matches)
            .Where(t => t.Status == TournamentStatus.Completed)
            .ToListAsync(ct);

        var gold = 0; var silver = 0; var bronze = 0;
        foreach (var t in tournaments)
        {
            var final = t.Matches
                .Where(m => m.Status == BracketMatchStatus.Completed)
                .OrderByDescending(m => m.Round)
                .FirstOrDefault();
            if (final is null) continue;

            if (final.Winner == request.MemberId) gold++;
            else if (final.ParticipantAId == request.MemberId || final.ParticipantBId == request.MemberId)
                silver++;
            else
            {
                var semiRound = final.Round - 1;
                var lostSemi = t.Matches.Any(m => m.Round == semiRound
                    && m.Status == BracketMatchStatus.Completed
                    && (m.ParticipantAId == request.MemberId || m.ParticipantBId == request.MemberId)
                    && m.Winner != request.MemberId);
                if (lostSemi) bronze++;
            }
        }

        var wins = 0; var losses = 0;
        foreach (var m in tournaments.SelectMany(t => t.Matches))
        {
            if (m.Status != BracketMatchStatus.Completed || m.Winner is null) continue;
            var a = m.ParticipantAId; var b = m.ParticipantBId;
            if (a != request.MemberId && b != request.MemberId) continue;

            if (m.Winner == request.MemberId) wins++;
            else losses++;
        }
        var totalMatches = wins + losses;
        var winRate = totalMatches == 0 ? 0 : Math.Round((double)wins / totalMatches, 3);

        var trend = wins + losses == 0
            ? "No recorded competitive bouts yet — development phase."
            : wins > losses
                ? $"Trending up — {wins} wins across {totalMatches} logged bouts."
                : wins == losses
                    ? $"Even record at {wins}-{losses}. Tournament experience will be the tiebreaker."
                    : $"Working through a setback — {wins} wins, {losses} losses. Coaching focus advised.";

        var outlook = winRate switch
        {
            >= 0.7 => "Strong favourite at next scheduled bout.",
            >= 0.5 => "Competitive at next bout — expect a close decision.",
            > 0    => "Underdog at next bout — look for a tactical gameplan.",
            _      => "Unrated until first sanctioned bout."
        };

        return new AthleteForm(
            member.Id, member.FullName, member.SMF_ID,
            totalMatches, wins, losses, winRate,
            gold, silver, bronze, trend, outlook);
    }
}

// ─── Federation pulse (exec dashboard) ──────────────────────────────────────

public sealed record GetFederationPulseQuery : IRequest<FederationPulse>;

public sealed class GetFederationPulseQueryHandler
    : IRequestHandler<GetFederationPulseQuery, FederationPulse>
{
    private readonly IApplicationDbContext _db;
    private readonly IScoringEventStore _events;

    public GetFederationPulseQueryHandler(IApplicationDbContext db, IScoringEventStore events)
    {
        _db = db;
        _events = events;
    }

    public async Task<FederationPulse> Handle(GetFederationPulseQuery request, CancellationToken ct)
    {
        var totalMembers = await _db.Members.CountAsync(ct);
        var approved = await _db.Members.CountAsync(m => m.RegistrationStatus == RegistrationStatus.Approved, ct);
        var activeClubs = await _db.Clubs.CountAsync(c => c.Status == ClubStatus.Active, ct);
        var upcoming = await _db.Events.CountAsync(e => e.StartsAtUtc > DateTime.UtcNow
                                                        && e.Status != EventStatus.Cancelled
                                                        && e.Status != EventStatus.Draft, ct);
        var publishedCourses = await _db.Courses.CountAsync(c => c.IsPublished && !c.IsArchived, ct);
        var enrollments = await _db.CourseEnrollments
            .CountAsync(e => e.Status == CourseEnrollmentStatus.Active, ct);

        var since = DateTime.UtcNow.AddDays(-30);
        var recentMatches = await _db.Matches.Where(m => m.CreatedAtUtc >= since).ToListAsync(ct);

        // Sum strikes across recent matches — small matches population so N
        // round-trips is acceptable here. Worst case we materialise a few
        // dozen guid lookups against the scoring store.
        var strikeTotal = 0;
        foreach (var m in recentMatches)
        {
            var s = await _events.GetStrikesAsync(m.Id, ct);
            strikeTotal += s.Count;
        }
        var avgStrikes = recentMatches.Count == 0
            ? 0
            : Math.Round((double)strikeTotal / recentMatches.Count, 1);

        var highlights = new List<string>();
        if (upcoming > 0) highlights.Add($"{upcoming} upcoming event{(upcoming == 1 ? "" : "s")} on the calendar.");
        if (activeClubs > 0) highlights.Add($"{activeClubs} accredited club{(activeClubs == 1 ? "" : "s")} in directory.");
        if (publishedCourses > 0) highlights.Add($"{publishedCourses} published course{(publishedCourses == 1 ? "" : "s")} in the learning library.");
        if (enrollments > 0) highlights.Add($"{enrollments} active learner{(enrollments == 1 ? "" : "s")}.");
        if (strikeTotal > 0) highlights.Add($"{strikeTotal} strikes logged in the last 30 days.");
        if (highlights.Count == 0) highlights.Add("Federation is in warm-up — seed data + first registrations landing.");

        return new FederationPulse(
            totalMembers, approved, activeClubs, upcoming,
            publishedCourses, enrollments,
            recentMatches.Count, strikeTotal, avgStrikes,
            highlights);
    }
}
