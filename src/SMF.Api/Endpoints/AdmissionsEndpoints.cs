using MediatR;
using Microsoft.AspNetCore.Mvc;
using SMF.Application.Features.Admissions.Commands.VerifyDigitalId;

namespace SMF.Api.Endpoints;

public static class AdmissionsEndpoints
{
    public static IEndpointRouteBuilder MapAdmissionsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admissions").WithTags("Admissions");

        // Gate-side verification. The gate scanner posts whatever it
        // got from the QR; we give back a precise outcome + live status
        // so the UI can show "Admitted / Deny — expired / Deny — revoked".
        //
        // Returns 200 OK for both admits and denies — the `outcome`
        // field is the contract, not the HTTP status. That way
        // operations dashboards can distinguish "scanner online but
        // token rejected" from "scanner can't reach the API" (which
        // surfaces as a 5xx / timeout).
        group.MapPost("/verify", async (
                [FromBody] VerifyDigitalIdCommand command,
                ISender sender,
                CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);
                return Results.Ok(result);
            })
            .WithName("VerifyDigitalId")
            .Produces<VerifyDigitalIdResult>(StatusCodes.Status200OK);

        return app;
    }
}
