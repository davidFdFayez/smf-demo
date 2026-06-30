using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SMF.Api.Hubs;
using SMF.Application.Features.Scoring;
using SMF.Domain.Entities;
using SMF.Domain.Enums;
using SMF.Infrastructure.Persistence;
using Xunit;

namespace SMF.IntegrationTests;

public sealed class MatchScoringHubTests : IClassFixture<SmfWebApplicationFactory>, IAsyncLifetime
{
    private static readonly TimeSpan BroadcastTimeout = TimeSpan.FromSeconds(10);

    private static readonly Guid HeadRefereeId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid SideRefereeAId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid SideRefereeBId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");
    private static readonly Guid UnassignedRefereeId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000009");

    private const string MatchCode = "hub-test-match-001";

    private readonly SmfWebApplicationFactory _factory;

    public MatchScoringHubTests(SmfWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Seed the referees as Members so the Matches.HeadRefereeId +
        // MatchReferees.RefereeId foreign keys are satisfied.
        SeedReferee(db, HeadRefereeId, "Head Ref", "#SMF-TEST-HR");
        SeedReferee(db, SideRefereeAId, "Side Ref A", "#SMF-TEST-SR-A");
        SeedReferee(db, SideRefereeBId, "Side Ref B", "#SMF-TEST-SR-B");
        SeedReferee(db, UnassignedRefereeId, "Not Assigned", "#SMF-TEST-NA");
        await db.SaveChangesAsync();

        if (!db.Matches.Any(m => m.Code == MatchCode))
        {
            var match = Match.Schedule(
                MatchCode,
                HeadRefereeId,
                DateTime.UtcNow.AddHours(1),
                DateTime.UtcNow);
            match.AssignReferee(SideRefereeAId);
            match.AssignReferee(SideRefereeBId);
            db.Matches.Add(match);
            await db.SaveChangesAsync();
        }
    }

    private static void SeedReferee(ApplicationDbContext db, Guid id, string name, string smfId)
    {
        if (db.Members.Any(m => m.Id == id)) return;
        db.Members.Add(Member.Seed(id, name, new DateOnly(1990, 1, 1), MemberRole.Referee, smfId));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // -------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------

    [Fact]
    public async Task SubmitStrike_FansOutToAllGroupMembers_AndPersistsEvent()
    {
        // One connection acts as the side referee producing strikes; two
        // more (dashboard + public scoreboard) are passive listeners.
        await using var sideRef = await ConnectAsync(SideRefereeAId);
        await using var dashboard = await ConnectAsync(HeadRefereeId);
        await using var scoreboard = await ConnectAsync(SideRefereeBId);

        var dashboardReceived = HookStrike(dashboard);
        var scoreboardReceived = HookStrike(scoreboard);

        await dashboard.InvokeAsync("JoinMatch", MatchCode);
        await scoreboard.InvokeAsync("JoinMatch", MatchCode);

        await sideRef.InvokeAsync("SubmitStrike", MatchCode, SideRefereeAId.ToString(), "Red");

        var dashboardPayload = await dashboardReceived.Task.WaitAsync(BroadcastTimeout);
        var scoreboardPayload = await scoreboardReceived.Task.WaitAsync(BroadcastTimeout);

        dashboardPayload.MatchId.Should().Be(MatchCode);
        dashboardPayload.RefereeId.Should().Be(SideRefereeAId.ToString());
        dashboardPayload.FighterColor.Should().Be(FighterColor.Red);
        scoreboardPayload.EventId.Should().Be(dashboardPayload.EventId);

        // Persistence guarantee: reconnecting clients must be able to replay.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.StrikeEvents
            .Count(e => e.RefereeId == SideRefereeAId && e.FighterColor == FighterColor.Red)
            .Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task OverrideScore_AllowedForHeadReferee_AndPersistsEvent()
    {
        await using var headRef = await ConnectAsync(HeadRefereeId);
        await using var dashboard = await ConnectAsync(SideRefereeAId);

        var received = HookOverride(dashboard);

        await dashboard.InvokeAsync("JoinMatch", MatchCode);

        await headRef.InvokeAsync(
            "OverrideScore",
            MatchCode,
            HeadRefereeId.ToString(),
            new ScoreOverride(Red: 10, Blue: 9, Round: 3));

        var payload = await received.Task.WaitAsync(BroadcastTimeout);

        payload.MatchId.Should().Be(MatchCode);
        payload.HeadRefereeId.Should().Be(HeadRefereeId.ToString());
        payload.NewScore.Red.Should().Be(10);
        payload.NewScore.Blue.Should().Be(9);
        payload.NewScore.Round.Should().Be(3);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ScoreOverrideEvents
            .Count(e => e.HeadRefereeId == HeadRefereeId && e.Red == 10 && e.Blue == 9)
            .Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task OverrideScore_RejectedForNonHeadReferee()
    {
        await using var sideRef = await ConnectAsync(SideRefereeAId);

        var act = async () => await sideRef.InvokeAsync(
            "OverrideScore",
            MatchCode,
            SideRefereeAId.ToString(),
            new ScoreOverride(Red: 1, Blue: 0));

        var ex = await act.Should().ThrowAsync<HubException>();
        ex.Which.Message.Should().Contain("head referee", "only the head referee may override");
    }

    [Fact]
    public async Task SubmitStrike_RejectedWhenRefereeIdDoesNotMatchCallerIdentity()
    {
        // Caller is authenticated as SideRefereeA but tries to submit as SideRefereeB.
        await using var sideRef = await ConnectAsync(SideRefereeAId);

        var act = async () => await sideRef.InvokeAsync(
            "SubmitStrike",
            MatchCode,
            SideRefereeBId.ToString(),
            "Blue");

        var ex = await act.Should().ThrowAsync<HubException>();
        ex.Which.Message.Should().Contain("identity");
    }

    [Fact]
    public async Task SubmitStrike_RejectedWhenRefereeNotAssignedToMatch()
    {
        await using var stranger = await ConnectAsync(UnassignedRefereeId);

        var act = async () => await stranger.InvokeAsync(
            "SubmitStrike",
            MatchCode,
            UnassignedRefereeId.ToString(),
            "Red");

        var ex = await act.Should().ThrowAsync<HubException>();
        ex.Which.Message.Should().Contain("not assigned");
    }

    [Fact]
    public async Task GetMatchReplay_ReturnsPersistedStrikeAndOverrideStream()
    {
        // Produce one strike + one override so we have both event types to replay.
        await using var sideRef = await ConnectAsync(SideRefereeAId);
        await using var headRef = await ConnectAsync(HeadRefereeId);

        await sideRef.InvokeAsync("SubmitStrike", MatchCode, SideRefereeAId.ToString(), "Blue");
        await headRef.InvokeAsync(
            "OverrideScore",
            MatchCode,
            HeadRefereeId.ToString(),
            new ScoreOverride(Red: 5, Blue: 7, Round: 2));

        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/matches/{MatchCode}/events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var replay = await response.Content.ReadFromJsonAsync<ReplayResponse>();

        replay.Should().NotBeNull();
        replay!.MatchCode.Should().Be(MatchCode);
        replay.Strikes.Should().NotBeEmpty(
            "at least the strike emitted above must be persisted + replayable");
        replay.Overrides.Should().Contain(o => o.Red == 5 && o.Blue == 7 && o.Round == 2);
    }

    [Fact]
    public async Task HubConnection_RejectedWithoutJwt()
    {
        var connection = BuildConnection(accessToken: null);

        var act = async () => await connection.StartAsync();

        await act.Should().ThrowAsync<HttpRequestException>();
        await connection.DisposeAsync();
    }

    // -------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------

    private async Task<HubConnection> ConnectAsync(Guid refereeId)
    {
        var token = await IssueDevTokenAsync(refereeId);
        var connection = BuildConnection(token);
        await connection.StartAsync();
        return connection;
    }

    private HubConnection BuildConnection(string? accessToken)
    {
        var hubUrl = new Uri(_factory.Server.BaseAddress, MatchScoringHub.Path);

        return new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                // Route hub traffic through the TestServer handler instead of a real socket.
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult(accessToken);
                // LongPolling is fully supported by TestServer without extra plumbing.
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
    }

    private async Task<string> IssueDevTokenAsync(Guid refereeId)
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/dev-token",
            new { RefereeId = refereeId, DisplayName = $"Test-{refereeId:N}" });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "dev-token endpoint must work in Testing environment");

        var body = await response.Content.ReadFromJsonAsync<TokenResponse>();
        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrEmpty();
        return body.AccessToken;
    }

    private static TaskCompletionSource<StrikeUpdatePayload> HookStrike(HubConnection conn)
    {
        var tcs = new TaskCompletionSource<StrikeUpdatePayload>(TaskCreationOptions.RunContinuationsAsynchronously);
        conn.On<StrikeUpdatePayload>("ReceiveStrikeUpdate", p => tcs.TrySetResult(p));
        return tcs;
    }

    private static TaskCompletionSource<ScoreOverridePayload> HookOverride(HubConnection conn)
    {
        var tcs = new TaskCompletionSource<ScoreOverridePayload>(TaskCreationOptions.RunContinuationsAsynchronously);
        conn.On<ScoreOverridePayload>("ReceiveScoreOverride", p => tcs.TrySetResult(p));
        return tcs;
    }

    private sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);

    private sealed record ReplayResponse(
        Guid MatchId,
        string MatchCode,
        IReadOnlyList<ReplayStrike> Strikes,
        IReadOnlyList<ReplayOverride> Overrides);

    private sealed record ReplayStrike(Guid EventId, Guid RefereeId, string FighterColor, DateTimeOffset OccurredAtUtc);

    private sealed record ReplayOverride(Guid EventId, Guid HeadRefereeId, int Red, int Blue, int? Round, DateTimeOffset OccurredAtUtc);
}
