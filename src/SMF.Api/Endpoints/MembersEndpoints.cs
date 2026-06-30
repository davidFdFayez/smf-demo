using MediatR;
using Microsoft.AspNetCore.Mvc;
using SMF.Application.Features.Members.Commands.ApproveMember;
using SMF.Application.Features.Members.Commands.IssueDigitalId;
using SMF.Application.Features.Members.Commands.RegisterMember;
using SMF.Application.Features.Members.Queries.GetMemberById;
using SMF.Application.Features.Members.Queries.GetMembers;
using SMF.Domain.Enums;

namespace SMF.Api.Endpoints;

public static class MembersEndpoints
{
    public static IEndpointRouteBuilder MapMembersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/members").WithTags("Members");

        group.MapPost("/", async (
                [FromBody] RegisterMemberCommand command,
                HttpContext http,
                ISender sender,
                CancellationToken ct) =>
            {
                // Enrich the command with trustworthy network metadata server-side
                // so the resulting ConsentLog audit row has an IP/UA that the client
                // cannot spoof.
                var enriched = command with
                {
                    IpAddress = command.IpAddress
                        ?? http.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = command.UserAgent
                        ?? http.Request.Headers.UserAgent.ToString(),
                };

                var result = await sender.Send(enriched, ct);
                return Results.Created($"/api/members/{result.Id}", result);
            })
            .WithName("RegisterMember")
            .Produces<RegisterMemberResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapGet("/", async (
                ISender sender,
                CancellationToken ct,
                [FromQuery] int page = 1,
                [FromQuery] int pageSize = 25,
                [FromQuery] string? search = null,
                [FromQuery] MemberRole? role = null,
                [FromQuery] RegistrationStatus? status = null) =>
            {
                var result = await sender.Send(
                    new GetMembersQuery(page, pageSize, search, role, status), ct);
                return Results.Ok(result);
            })
            .WithName("ListMembers")
            .Produces<PagedResult<MemberListItem>>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        // Single-member fetch — drives the athlete mobile client's
        // "refresh profile" action after login and whenever connectivity
        // is restored. Returns 404 if the id is unknown.
        group.MapGet("/{id:guid}", async (
                Guid id,
                ISender sender,
                CancellationToken ct) =>
            {
                var result = await sender.Send(new GetMemberByIdQuery(id), ct);
                return Results.Ok(result);
            })
            .WithName("GetMemberById")
            .Produces<MemberDetails>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Issue a signed Digital ID token. The mobile client stores
        // this in Hive so the QR works offline at the venue gate; the
        // gate scanner verifies the signature (online) and re-checks
        // live status to catch post-issue revocations.
        group.MapPost("/{id:guid}/digital-id", async (
                Guid id,
                ISender sender,
                CancellationToken ct) =>
            {
                var result = await sender.Send(new IssueDigitalIdCommand(id), ct);
                return Results.Ok(result);
            })
            .WithName("IssueMemberDigitalId")
            .Produces<IssueDigitalIdResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/approve", async (
                Guid id,
                ISender sender,
                CancellationToken ct) =>
            {
                await sender.Send(new ApproveMemberCommand(id), ct);
                return Results.NoContent();
            })
            .WithName("ApproveMember")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
