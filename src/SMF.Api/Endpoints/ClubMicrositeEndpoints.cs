using MediatR;
using Microsoft.AspNetCore.Mvc;
using SMF.Application.Features.Clubs;

namespace SMF.Api.Endpoints;

/// <summary>
/// Public + admin endpoints for the "Club microsite" advanced feature
/// (PDF §9). The <c>/api/clubs/by-slug/{slug}</c> route is the public
/// entry point and returns 404 for non-active clubs; the admin PUT updates
/// the microsite fields for any club, regardless of approval status.
/// </summary>
public static class ClubMicrositeEndpoints
{
    public static IEndpointRouteBuilder MapClubMicrositeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/clubs").WithTags("Club Microsites");

        group.MapGet("/by-slug/{slug}", async (
                string slug, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetClubMicrositeQuery(slug), ct)))
            .WithName("GetClubMicrosite")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/microsite", async (
                Guid id,
                [FromBody] ClubMicrositeBody body,
                ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new UpdateClubMicrositeCommand(
                id, body.Headline, body.About, body.HeroImageUrl,
                body.PrimaryColor, body.InstagramHandle,
                body.TwitterHandle, body.YoutubeChannel), ct)))
            .WithName("UpdateClubMicrosite")
            .ProducesValidationProblem();

        return app;
    }

    public sealed record ClubMicrositeBody(
        string? Headline, string? About, string? HeroImageUrl,
        string? PrimaryColor, string? InstagramHandle,
        string? TwitterHandle, string? YoutubeChannel);
}
