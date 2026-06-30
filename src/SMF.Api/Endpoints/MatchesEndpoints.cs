using MediatR;
using Microsoft.AspNetCore.Mvc;
using SMF.Application.Features.Matches.Commands.AssignReferee;
using SMF.Application.Features.Matches.Commands.AssignTimekeeper;
using SMF.Application.Features.Matches.Commands.ScheduleMatch;
using SMF.Application.Features.Matches.Queries.GetMatchReplay;
using SMF.Application.Features.Matches.Queries.ListMatches;

namespace SMF.Api.Endpoints;

public static class MatchesEndpoints
{
    public sealed record ScheduleMatchRequest(
        string Code,
        Guid HeadRefereeId,
        DateTime ScheduledAtUtc,
        Guid[]? SideRefereeIds = null);

    public sealed record AssignRefereeRequest(Guid RefereeId);

    public sealed record AssignTimekeeperRequest(Guid TimekeeperId);

    public static void MapMatchesEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/matches").WithTags("Matches");

        group.MapGet("/", async (IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.Send(new ListMatchesQuery(), ct)))
            .WithName("ListMatches")
            .WithSummary("Lists scheduled, live, and completed matches for public pickers.");

        group.MapPost("/", async (
            [FromBody] ScheduleMatchRequest req,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new ScheduleMatchCommand(
                req.Code,
                req.HeadRefereeId,
                req.ScheduledAtUtc,
                req.SideRefereeIds), ct);

            return Results.Created($"/api/matches/{result.Code}", result);
        })
        .WithName("ScheduleMatch");

        group.MapPost("/{code}/referees", async (
            string code,
            [FromBody] AssignRefereeRequest req,
            IMediator mediator,
            CancellationToken ct) =>
        {
            await mediator.Send(new AssignRefereeCommand(code, req.RefereeId), ct);
            return Results.NoContent();
        })
        .WithName("AssignMatchReferee");

        group.MapPost("/{code}/timekeeper", async (
            string code,
            [FromBody] AssignTimekeeperRequest req,
            IMediator mediator,
            CancellationToken ct) =>
        {
            await mediator.Send(new AssignTimekeeperCommand(code, req.TimekeeperId), ct);
            return Results.NoContent();
        })
        .WithName("AssignMatchTimekeeper")
        .WithSummary("Assign the timekeeper who drives the round clock for this match.");

        group.MapGet("/{code}/events", async (
            string code,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var replay = await mediator.Send(new GetMatchReplayQuery(code), ct);
            return Results.Ok(replay);
        })
        .WithName("GetMatchReplay")
        .WithSummary("Returns the full append-only strike + override stream for late-joining scoreboards.");
    }
}
