using MediatR;
using SMF.Application.Features.Analytics;

namespace SMF.Api.Endpoints;

public static class AnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/analytics").WithTags("AI Analytics");

        group.MapGet("/match/{code}", async (
                string code, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetMatchInsightsQuery(code), ct)))
            .WithName("GetMatchInsights")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/athlete/{id:guid}/form", async (
                Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetAthleteFormQuery(id), ct)))
            .WithName("GetAthleteForm")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/pulse", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetFederationPulseQuery(), ct)))
            .WithName("GetFederationPulse");

        return app;
    }
}
