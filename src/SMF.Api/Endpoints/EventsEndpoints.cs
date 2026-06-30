using MediatR;
using Microsoft.AspNetCore.Mvc;
using SMF.Application.Features.Events;
using SMF.Domain.Enums;

namespace SMF.Api.Endpoints;

public static class EventsEndpoints
{
    public static IEndpointRouteBuilder MapEventsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/events").WithTags("Events");

        group.MapPost("/", async ([FromBody] CreateEventCommand cmd, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(cmd, ct);
            return Results.Created($"/api/events/{result.Id}", result);
        }).WithName("CreateEvent").ProducesValidationProblem();

        group.MapGet("/", async ([FromQuery] EventStatus? status, ISender sender, CancellationToken ct)
            => Results.Ok(await sender.Send(new ListEventsQuery(status), ct)))
            .WithName("ListEvents");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct)
            => Results.Ok(await sender.Send(new GetEventByIdQuery(id), ct)))
            .WithName("GetEventById").ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
                Guid id,
                [FromBody] UpdateEventBody body,
                ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new UpdateEventCommand(
                id,
                body.Title, body.Description, body.Location,
                body.StartsAtUtc, body.EndsAtUtc,
                body.RegistrationOpensAtUtc, body.RegistrationClosesAtUtc,
                body.EntryFeeMinor, body.Currency, body.Capacity), ct);
            return Results.Ok(result);
        }).WithName("UpdateEvent").ProducesValidationProblem();

        group.MapPost("/{id:guid}/publish", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new PublishEventCommand(id), ct);
            return Results.NoContent();
        }).WithName("PublishEvent");

        group.MapPost("/{id:guid}/cancel", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new CancelEventCommand(id), ct);
            return Results.NoContent();
        }).WithName("CancelEvent");

        group.MapPost("/{id:guid}/registrations", async (
                Guid id,
                [FromBody] RegisterForEventBody body,
                ISender sender, CancellationToken ct) =>
        {
            var regId = await sender.Send(new RegisterForEventCommand(id, body.MemberId), ct);
            return Results.Created($"/api/events/{id}/registrations/{regId}", new { registrationId = regId });
        }).WithName("RegisterForEvent").ProducesValidationProblem();

        group.MapGet("/{id:guid}/registrations", async (Guid id, ISender sender, CancellationToken ct)
            => Results.Ok(await sender.Send(new ListEventRegistrationsQuery(id), ct)))
            .WithName("ListEventRegistrations");

        group.MapPost("/registrations/{regId:guid}/check-in", async (
                Guid regId, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new CheckInRegistrationCommand(regId), ct);
            return Results.NoContent();
        }).WithName("CheckInEventRegistration");

        group.MapPost("/registrations/{regId:guid}/no-show", async (
                Guid regId, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new MarkNoShowCommand(regId), ct);
            return Results.NoContent();
        }).WithName("MarkNoShow");

        group.MapPost("/registrations/{regId:guid}/cancel", async (
                Guid regId, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new CancelRegistrationCommand(regId), ct);
            return Results.NoContent();
        }).WithName("CancelEventRegistration");

        return app;
    }

    public sealed record RegisterForEventBody(Guid MemberId);

    public sealed record UpdateEventBody(
        string Title,
        string? Description,
        string Location,
        DateTime StartsAtUtc,
        DateTime EndsAtUtc,
        DateTime RegistrationOpensAtUtc,
        DateTime RegistrationClosesAtUtc,
        long EntryFeeMinor,
        string Currency,
        int? Capacity);
}
