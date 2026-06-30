using MediatR;
using Microsoft.AspNetCore.Mvc;
using SMF.Application.Features.Safeguarding;
using SMF.Domain.Enums;

namespace SMF.Api.Endpoints;

public static class SafeguardingEndpoints
{
    public static IEndpointRouteBuilder MapSafeguardingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/safeguarding").WithTags("Safeguarding");

        group.MapPost("/reports", async (
                [FromBody] SubmitSafeguardingReportCommand cmd,
                ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(cmd, ct);
            return Results.Created(
                $"/api/safeguarding/reports/by-code/{result.ReferenceCode}", result);
        })
            .WithName("SubmitSafeguardingReport")
            .ProducesValidationProblem();

        group.MapGet("/reports", async (
                [FromQuery] SafeguardingReportStatus? status,
                [FromQuery] int? limit,
                ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(
                new ListSafeguardingReportsQuery(status, limit is null or <= 0 ? 100 : limit.Value), ct)))
            .WithName("ListSafeguardingReports");

        group.MapGet("/reports/by-code/{code}", async (
                string code, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetSafeguardingReportByCodeQuery(code), ct)))
            .WithName("GetSafeguardingReportByCode")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/reports/{id:guid}/triage", async (
                Guid id,
                [FromBody] TriageRequest body,
                ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(
                new TriageSafeguardingReportCommand(id, body.NewStatus, body.ReviewerNotes), ct)))
            .WithName("TriageSafeguardingReport");

        return app;
    }

    public sealed record TriageRequest(
        SafeguardingReportStatus NewStatus,
        string? ReviewerNotes);
}
