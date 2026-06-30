using MediatR;
using Microsoft.AspNetCore.Mvc;
using SMF.Application.Features.Tournaments;

namespace SMF.Api.Endpoints;

public static class TournamentsEndpoints
{
    public static IEndpointRouteBuilder MapTournamentsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tournaments").WithTags("Tournaments");

        group.MapPost("/", async ([FromBody] GenerateTournamentCommand cmd, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(cmd, ct);
            return Results.Created($"/api/tournaments/{result.Id}", result);
        }).WithName("GenerateTournament").ProducesValidationProblem();

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct)
            => Results.Ok(await sender.Send(new GetTournamentQuery(id), ct)))
            .WithName("GetTournament").ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/by-event/{eventId:guid}", async (Guid eventId, ISender sender, CancellationToken ct)
            => Results.Ok(await sender.Send(new ListTournamentsForEventQuery(eventId), ct)))
            .WithName("ListTournamentsForEvent");

        group.MapPost("/{id:guid}/matches/{matchId:guid}/result", async (
                Guid id, Guid matchId, [FromBody] RecordResultBody body,
                ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new RecordMatchResultCommand(id, matchId, body.WinnerIsA), ct);
            return Results.NoContent();
        }).WithName("RecordTournamentMatchResult");

        group.MapPost("/{id:guid}/matches/{matchId:guid}/no-show", async (
                Guid id, Guid matchId, [FromBody] NoShowBody body,
                ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new MarkBracketNoShowCommand(id, matchId, body.WalkoverToA), ct);
            return Results.NoContent();
        }).WithName("MarkTournamentNoShow");

        return app;
    }

    public sealed record RecordResultBody(bool WinnerIsA);
    public sealed record NoShowBody(bool? WalkoverToA);
}
