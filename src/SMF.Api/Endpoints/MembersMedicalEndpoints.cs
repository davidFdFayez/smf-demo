using MediatR;
using Microsoft.AspNetCore.Mvc;
using SMF.Application.Features.Members.Commands.RecordMedicalClearance;

namespace SMF.Api.Endpoints;

/// <summary>
/// Members sub-routes concerned with athlete readiness (PDF §5 "Athlete
/// Management"). Deliberately kept in a small dedicated file so the main
/// members endpoints module doesn't grow unbounded as more read/write
/// surfaces are added later (injuries, gear certification, etc.).
/// </summary>
public static class MembersMedicalEndpoints
{
    public static IEndpointRouteBuilder MapMembersMedicalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/members").WithTags("Members");

        group.MapPost("/{id:guid}/medical-clearance", async (
                Guid id,
                [FromBody] MedicalClearanceRequest body,
                ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new RecordMedicalClearanceCommand(
                id, body.Cleared, body.WeightCategoryKg), ct);
            return Results.NoContent();
        })
            .WithName("RecordMedicalClearance")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    public sealed record MedicalClearanceRequest(bool Cleared, decimal? WeightCategoryKg);
}
