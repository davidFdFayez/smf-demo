using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Rankings;

// ─── DTOs ───────────────────────────────────────────────────────────────────

public sealed record AthleteRanking(
    Guid MemberId,
    string SMF_ID,
    string FullName,
    int Gold,
    int Silver,
    int Bronze,
    int TotalMedals,
    int MatchesWon,
    int MatchesLost,
    Guid? AffiliatedClubId);

public sealed record TournamentStanding(
    Guid TournamentId,
    Guid EventId,
    string Title,
    string Division,
    TournamentStatus Status,
    Guid? ChampionId,
    string? ChampionName,
    Guid? RunnerUpId,
    string? RunnerUpName,
    IReadOnlyList<Guid> SemiFinalistIds,
    IReadOnlyList<string> SemiFinalistNames);

// ─── Top athletes by medal count ─────────────────────────────────────────────

public sealed record GetAthleteRankingsQuery(int Limit = 50)
    : IRequest<IReadOnlyList<AthleteRanking>>;

public sealed class GetAthleteRankingsQueryHandler
    : IRequestHandler<GetAthleteRankingsQuery, IReadOnlyList<AthleteRanking>>
{
    private readonly IApplicationDbContext _db;

    public GetAthleteRankingsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<AthleteRanking>> Handle(
        GetAthleteRankingsQuery request,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(request.Limit, 1, 500);

        // Pull all completed tournaments with their brackets in a single
        // round-trip, then compute medal standings in memory. This keeps the
        // SQL simple and the projection logic localised; it's fine for a
        // federation-scale dataset (thousands of tournaments, not millions).
        var tournaments = await _db.Tournaments
            .AsNoTracking()
            .Include(t => t.Matches)
            .Where(t => t.Status == TournamentStatus.Completed)
            .ToListAsync(cancellationToken);

        var standings = tournaments
            .Select(ComputeTournamentStanding)
            .ToList();

        // Bucket each athlete's finishes across all tournaments.
        var medals = new Dictionary<Guid, (int gold, int silver, int bronze)>();
        foreach (var s in standings)
        {
            if (s.ChampionId is { } champ)
                Inc(medals, champ, gold: 1);
            if (s.RunnerUpId is { } ru)
                Inc(medals, ru, silver: 1);
            foreach (var sf in s.SemiFinalistIds)
                Inc(medals, sf, bronze: 1);
        }

        // Match win/loss totals from all BracketMatches.
        var wins = new Dictionary<Guid, int>();
        var losses = new Dictionary<Guid, int>();
        foreach (var t in tournaments)
        {
            foreach (var m in t.Matches)
            {
                if (m.Status != BracketMatchStatus.Completed) continue;
                if (m.Winner is not { } w) continue;
                wins[w] = wins.GetValueOrDefault(w) + 1;

                var loser = m.ParticipantAId == w ? m.ParticipantBId : m.ParticipantAId;
                if (loser is { } l && l != Guid.Empty)
                    losses[l] = losses.GetValueOrDefault(l) + 1;
            }
        }

        // Join with member info for display.
        var ids = medals.Keys
            .Union(wins.Keys)
            .Union(losses.Keys)
            .ToHashSet();

        if (ids.Count == 0) return Array.Empty<AthleteRanking>();

        var members = await _db.Members
            .AsNoTracking()
            .Where(m => ids.Contains(m.Id) && m.Role == MemberRole.Athlete)
            .Select(m => new { m.Id, m.SMF_ID, m.FullName, m.AffiliatedClubId })
            .ToListAsync(cancellationToken);

        var ranked = members
            .Select(m =>
            {
                var med = medals.GetValueOrDefault(m.Id);
                return new AthleteRanking(
                    MemberId: m.Id,
                    SMF_ID: m.SMF_ID,
                    FullName: m.FullName,
                    Gold: med.gold,
                    Silver: med.silver,
                    Bronze: med.bronze,
                    TotalMedals: med.gold + med.silver + med.bronze,
                    MatchesWon: wins.GetValueOrDefault(m.Id),
                    MatchesLost: losses.GetValueOrDefault(m.Id),
                    AffiliatedClubId: m.AffiliatedClubId);
            })
            .OrderByDescending(r => r.Gold)
            .ThenByDescending(r => r.Silver)
            .ThenByDescending(r => r.Bronze)
            .ThenByDescending(r => r.MatchesWon)
            .ThenBy(r => r.FullName)
            .Take(limit)
            .ToList();

        return ranked;

        static void Inc(Dictionary<Guid, (int gold, int silver, int bronze)> dict,
                        Guid id, int gold = 0, int silver = 0, int bronze = 0)
        {
            var cur = dict.GetValueOrDefault(id);
            dict[id] = (cur.gold + gold, cur.silver + silver, cur.bronze + bronze);
        }
    }

    private static TournamentStanding ComputeTournamentStanding(Domain.Entities.Tournament t)
    {
        // Final match: highest round, single entry.
        var final = t.Matches
            .Where(m => m.Status == BracketMatchStatus.Completed)
            .OrderByDescending(m => m.Round)
            .ThenBy(m => m.OrderInRound)
            .FirstOrDefault();

        Guid? champion = null;
        Guid? runnerUp = null;
        if (final is not null && final.Winner is { } w)
        {
            champion = w;
            runnerUp = final.ParticipantAId == w ? final.ParticipantBId : final.ParticipantAId;
        }

        // Semi-finalists = losers of the round below the final (bronze medal
        // in a single-elimination bracket with both SF losers taking bronze,
        // matching IFMA and Olympic convention).
        var semiRound = final?.Round - 1;
        var semiLosers = new List<Guid>();
        if (semiRound is int r && r >= 1)
        {
            foreach (var m in t.Matches.Where(m => m.Round == r
                                                   && m.Status == BracketMatchStatus.Completed
                                                   && m.Winner is not null))
            {
                var loser = m.ParticipantAId == m.Winner ? m.ParticipantBId : m.ParticipantAId;
                if (loser is { } l && l != Guid.Empty) semiLosers.Add(l);
            }
        }

        return new TournamentStanding(
            TournamentId: t.Id,
            EventId: t.EventId,
            Title: t.Title,
            Division: t.Division,
            Status: t.Status,
            ChampionId: champion,
            ChampionName: null,
            RunnerUpId: runnerUp,
            RunnerUpName: null,
            SemiFinalistIds: semiLosers,
            SemiFinalistNames: Array.Empty<string>());
    }
}

// ─── Per-tournament standing ────────────────────────────────────────────────

public sealed record GetTournamentStandingQuery(Guid TournamentId)
    : IRequest<TournamentStanding>;

public sealed class GetTournamentStandingQueryHandler
    : IRequestHandler<GetTournamentStandingQuery, TournamentStanding>
{
    private readonly IApplicationDbContext _db;

    public GetTournamentStandingQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<TournamentStanding> Handle(
        GetTournamentStandingQuery request,
        CancellationToken cancellationToken)
    {
        var tournament = await _db.Tournaments
            .AsNoTracking()
            .Include(t => t.Matches)
            .FirstOrDefaultAsync(t => t.Id == request.TournamentId, cancellationToken)
            ?? throw new Application.Common.Exceptions.NotFoundException(
                "Tournament", request.TournamentId);

        var standing = GetAthleteRankingsQueryHandlerHelpers.Compute(tournament);

        // Resolve names so the UI doesn't have to do a second lookup.
        var ids = new List<Guid>();
        if (standing.ChampionId is { } c) ids.Add(c);
        if (standing.RunnerUpId is { } ru) ids.Add(ru);
        ids.AddRange(standing.SemiFinalistIds);

        var map = await _db.Members
            .AsNoTracking()
            .Where(m => ids.Contains(m.Id))
            .Select(m => new { m.Id, m.FullName })
            .ToDictionaryAsync(m => m.Id, m => m.FullName, cancellationToken);

        return standing with
        {
            ChampionName = standing.ChampionId is { } cc ? map.GetValueOrDefault(cc) : null,
            RunnerUpName = standing.RunnerUpId is { } rr ? map.GetValueOrDefault(rr) : null,
            SemiFinalistNames = standing.SemiFinalistIds
                .Select(id => map.GetValueOrDefault(id) ?? string.Empty)
                .ToList(),
        };
    }
}

// Internal helper so the per-tournament query can reuse the standings logic
// without the handler needing to duplicate it.
internal static class GetAthleteRankingsQueryHandlerHelpers
{
    public static TournamentStanding Compute(Domain.Entities.Tournament t)
    {
        var final = t.Matches
            .Where(m => m.Status == BracketMatchStatus.Completed)
            .OrderByDescending(m => m.Round)
            .ThenBy(m => m.OrderInRound)
            .FirstOrDefault();

        Guid? champion = null;
        Guid? runnerUp = null;
        if (final is not null && final.Winner is { } w)
        {
            champion = w;
            runnerUp = final.ParticipantAId == w ? final.ParticipantBId : final.ParticipantAId;
        }

        var semiRound = final?.Round - 1;
        var semiLosers = new List<Guid>();
        if (semiRound is int r && r >= 1)
        {
            foreach (var m in t.Matches.Where(m => m.Round == r
                                                   && m.Status == BracketMatchStatus.Completed
                                                   && m.Winner is not null))
            {
                var loser = m.ParticipantAId == m.Winner ? m.ParticipantBId : m.ParticipantAId;
                if (loser is { } l && l != Guid.Empty) semiLosers.Add(l);
            }
        }

        return new TournamentStanding(
            TournamentId: t.Id,
            EventId: t.EventId,
            Title: t.Title,
            Division: t.Division,
            Status: t.Status,
            ChampionId: champion,
            ChampionName: null,
            RunnerUpId: runnerUp,
            RunnerUpName: null,
            SemiFinalistIds: semiLosers,
            SemiFinalistNames: Array.Empty<string>());
    }
}
