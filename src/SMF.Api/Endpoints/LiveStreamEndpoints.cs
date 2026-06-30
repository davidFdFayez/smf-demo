using MediatR;
using Microsoft.AspNetCore.Mvc;
using SMF.Application.Features.LiveStreams;
using SMF.Domain.Enums;

namespace SMF.Api.Endpoints;

public static class LiveStreamEndpoints
{
    public static IEndpointRouteBuilder MapLiveStreamEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/live-streams").WithTags("Live Streaming");

        group.MapPut("/events/{eventId:guid}", async (
                Guid eventId, [FromBody] StreamBody body,
                ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(
                new SetEventLiveStreamCommand(eventId, body.Provider, body.Url), ct)))
            .WithName("SetEventLiveStream")
            .ProducesValidationProblem();

        group.MapPut("/matches/{matchId:guid}", async (
                Guid matchId, [FromBody] StreamBody body,
                ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(
                new SetMatchLiveStreamCommand(matchId, body.Provider, body.Url), ct)))
            .WithName("SetMatchLiveStream");

        group.MapGet("/matches/by-code/{code}", async (
                string code, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetMatchLiveStreamByCodeQuery(code), ct)))
            .WithName("GetMatchLiveStreamByCode")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    public sealed record StreamBody(LiveStreamProvider? Provider, string? Url);
}
