using MediatR;
using Microsoft.AspNetCore.Mvc;
using SMF.Application.Features.Notifications;

namespace SMF.Api.Endpoints;

public static class NotificationsEndpoints
{
    public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications").WithTags("Notifications");

        group.MapPost("/send", async ([FromBody] SendNotificationCommand cmd, ISender sender, CancellationToken ct)
            => Results.Ok(await sender.Send(cmd, ct)))
            .WithName("SendNotification")
            .ProducesValidationProblem();

        group.MapPost("/campaign", async ([FromBody] SendCampaignCommand cmd, ISender sender, CancellationToken ct)
            => Results.Ok(await sender.Send(cmd, ct)))
            .WithName("SendNotificationCampaign")
            .ProducesValidationProblem();

        return app;
    }
}
