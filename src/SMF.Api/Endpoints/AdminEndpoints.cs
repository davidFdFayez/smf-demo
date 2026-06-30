using MediatR;
using SMF.Application.Features.Admin;

namespace SMF.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin").WithTags("Admin");

        group.MapGet("/stats", async (ISender sender, CancellationToken ct)
            => Results.Ok(await sender.Send(new GetAdminStatsQuery(), ct)))
            .WithName("GetAdminStats");

        return app;
    }
}
