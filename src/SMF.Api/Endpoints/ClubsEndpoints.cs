using MediatR;
using Microsoft.AspNetCore.Mvc;
using SMF.Application.Features.Clubs;
using SMF.Domain.Enums;

namespace SMF.Api.Endpoints;

public static class ClubsEndpoints
{
    public static IEndpointRouteBuilder MapClubsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/clubs").WithTags("Clubs");

        group.MapPost("/", async ([FromBody] RegisterClubCommand cmd, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(cmd, ct);
            return Results.Created($"/api/clubs/{result.Slug}", result);
        })
            .WithName("RegisterClub")
            .ProducesValidationProblem();

        group.MapGet("/", async (
                [FromQuery] ClubStatus? status,
                ISender sender, CancellationToken ct)
            => Results.Ok(await sender.Send(new ListClubsQuery(status), ct)))
            .WithName("ListClubs");

        group.MapGet("/{slug}", async (string slug, ISender sender, CancellationToken ct)
            => Results.Ok(await sender.Send(new GetClubBySlugQuery(slug), ct)))
            .WithName("GetClubBySlug")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/approve", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ApproveClubCommand(id), ct);
            return Results.NoContent();
        }).WithName("ApproveClub");

        return app;
    }
}
