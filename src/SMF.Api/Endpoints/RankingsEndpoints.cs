using MediatR;
using Microsoft.AspNetCore.Mvc;
using SMF.Application.Features.Rankings;

namespace SMF.Api.Endpoints;

public static class RankingsEndpoints
{
    public static IEndpointRouteBuilder MapRankingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/rankings").WithTags("Rankings");

        group.MapGet("/athletes", async (
                [FromQuery] int? limit,
                ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(
                new GetAthleteRankingsQuery(limit is null or <= 0 ? 50 : limit.Value), ct)))
            .WithName("GetAthleteRankings");

        group.MapGet("/tournaments/{tournamentId:guid}", async (
                Guid tournamentId, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(
                new GetTournamentStandingQuery(tournamentId), ct)))
            .WithName("GetTournamentStanding")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
