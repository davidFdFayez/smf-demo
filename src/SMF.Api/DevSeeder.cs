using Microsoft.EntityFrameworkCore;
using SMF.Domain.Entities;
using SMF.Domain.Enums;
using SMF.Infrastructure.Persistence;

namespace SMF.Api;

/// <summary>
/// Populates demo matches and referee identities so developers (and demo
/// viewers) can open the watch page, drive scoring from admin, and see
/// AI insights without hand-wiring data first.
/// </summary>
internal static class DevSeeder
{
    public static readonly Guid HeadRefereeId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static readonly Guid[] SideRefereeIds =
    {
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Guid.Parse("33333333-3333-3333-3333-333333333333"),
        Guid.Parse("44444444-4444-4444-4444-444444444444")
    };

    public const string SampleMatchCode = "match-001";
    public const string DemoLiveMatchCode = "match-002";

    public static async Task SeedAsync(ApplicationDbContext db, ILogger logger)
    {
        await SeedRefereeAsync(db, HeadRefereeId, "Head Referee", "#SMF2026-SEED-HR");
        for (var i = 0; i < SideRefereeIds.Length; i++)
        {
            await SeedRefereeAsync(
                db,
                SideRefereeIds[i],
                $"Side Referee {i + 1}",
                $"#SMF2026-SEED-SR{i + 1:D2}");
        }
        await db.SaveChangesAsync();

        await EnsureMatchAsync(db, SampleMatchCode, DateTime.UtcNow.AddHours(1));
        await EnsureMatchAsync(db, DemoLiveMatchCode, DateTime.UtcNow);
        await db.SaveChangesAsync();

        await EnsureRecentDemoStrikesAsync(db, DemoLiveMatchCode, logger);

        logger.LogInformation(
            "Dev seed ready — sample matches: {Legacy}, {Live}.",
            SampleMatchCode, DemoLiveMatchCode);
    }

    private static async Task EnsureMatchAsync(
        ApplicationDbContext db,
        string code,
        DateTime scheduledAtUtc)
    {
        if (await db.Matches.AnyAsync(m => m.Code == code))
            return;

        var match = Match.Schedule(code, HeadRefereeId, scheduledAtUtc, DateTime.UtcNow);
        foreach (var id in SideRefereeIds)
            match.AssignReferee(id);

        db.Matches.Add(match);
    }

    /// <summary>
    /// Seeds a short burst of recent strikes so the public watch page shows a
    /// live scoreboard + AI insights for <see cref="DemoLiveMatchCode"/>.
    /// </summary>
    private static async Task EnsureRecentDemoStrikesAsync(
        ApplicationDbContext db,
        string code,
        ILogger logger)
    {
        var match = await db.Matches.AsNoTracking().FirstOrDefaultAsync(m => m.Code == code);
        if (match is null) return;

        const int targetStrikes = 24;
        var existing = await db.StrikeEvents.CountAsync(s => s.MatchId == match.Id);
        if (existing >= targetStrikes)
            return;

        var now = DateTime.UtcNow;
        var referee = SideRefereeIds[0];
        var strikes = new List<StrikeEvent>();
        for (var i = existing; i < targetStrikes; i++)
        {
            var color = i % 3 == 0 ? FighterColor.Blue : FighterColor.Red;
            strikes.Add(StrikeEvent.Record(
                match.Id,
                referee,
                color,
                now.AddMinutes(-(targetStrikes - i))));
        }

        db.StrikeEvents.AddRange(strikes);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Seeded {Count} recent demo strikes for '{Code}'.",
            strikes.Count, code);
    }

    private static async Task SeedRefereeAsync(
        ApplicationDbContext db,
        Guid id,
        string fullName,
        string smfId)
    {
        if (await db.Members.AnyAsync(m => m.Id == id))
            return;

        db.Members.Add(Member.Seed(
            id,
            fullName,
            new DateOnly(1990, 1, 1),
            MemberRole.Referee,
            smfId));
    }
}
