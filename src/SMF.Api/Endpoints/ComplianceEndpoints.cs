using MediatR;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Interfaces;
using SMF.Application.Features.Compliance.Common;
using SMF.Application.Features.Compliance.Governance;
using SMF.Application.Features.Compliance.ParentalConsent;
using SMF.Application.Features.Compliance.Policies;
using SMF.Domain.Enums;

namespace SMF.Api.Endpoints;

/// <summary>
/// Compliance & governance HTTP surface:
///
///   * Public:
///       <c>GET    /api/governance/documents</c>          — published list.
///       <c>GET    /api/governance/documents/{id}</c>     — published detail.
///       <c>GET    /api/governance/documents/{id}/download</c> — stream file.
///       <c>GET    /api/policies</c>                      — active terms / privacy / code of conduct.
///       <c>GET    /api/parental-consent/{id}/{token}</c> — fetch task for guardian.
///       <c>POST   /api/parental-consent/{id}/{token}</c> — guardian decides.
///   * Member:
///       <c>POST   /api/policies/accept</c>               — record acceptance for current member.
///   * Admin:
///       <c>POST   /api/admin/governance/documents</c>          — upload (multipart).
///       <c>PUT    /api/admin/governance/documents/{id}</c>     — edit metadata.
///       <c>POST   /api/admin/governance/documents/{id}/publish</c>
///       <c>POST   /api/admin/governance/documents/{id}/unpublish</c>
///       <c>DELETE /api/admin/governance/documents/{id}</c>
///       <c>GET    /api/admin/governance/documents</c>          — full list (drafts).
///       <c>POST   /api/admin/policies</c>                       — publish new version.
///       <c>GET    /api/admin/policies</c>                       — version history.
///       <c>GET    /api/admin/policies/acceptances</c>           — audit query.
///       <c>GET    /api/admin/parental-consents</c>              — moderation queue.
///       <c>GET    /api/admin/consent-logs</c>                   — append-only audit log.
/// </summary>
public static class ComplianceEndpoints
{
    public static IEndpointRouteBuilder MapComplianceEndpoints(this IEndpointRouteBuilder app)
    {
        MapPublic(app);
        MapMember(app);
        MapAdmin(app);
        return app;
    }

    // ──────────────────────────────────────── public surface
    private static void MapPublic(IEndpointRouteBuilder app)
    {
        var pub = app.MapGroup("/api").WithTags("Compliance");

        pub.MapGet("/governance/documents",
            async (IMediator m, GovernanceDocumentType? type, int? year, CancellationToken ct) =>
                Results.Ok(await m.Send(new GovernanceFeatures.ListGovernanceDocumentsQuery(true, type, year), ct)));

        pub.MapGet("/governance/documents/{id:guid}",
            async (Guid id, IMediator m, CancellationToken ct) =>
            {
                var doc = await m.Send(new GovernanceFeatures.GetGovernanceDocumentQuery(id, true), ct);
                return doc is null ? Results.NotFound() : Results.Ok(doc);
            });

        pub.MapGet("/governance/documents/{id:guid}/download",
            async (Guid id, IMediator m, IFileStorage storage, IApplicationDbContext db, CancellationToken ct) =>
            {
                var doc = await m.Send(new GovernanceFeatures.GetGovernanceDocumentQuery(id, true), ct);
                if (doc is null) return Results.NotFound();

                var key = await db.GovernanceDocuments
                    .Where(d => d.Id == id)
                    .Select(d => d.StorageKey)
                    .FirstOrDefaultAsync(ct);
                if (string.IsNullOrEmpty(key)) return Results.NotFound();

                var stream = await storage.OpenAsync(key, ct);
                return stream is null
                    ? Results.NotFound()
                    : Results.File(stream.Content, stream.ContentType, doc.OriginalFileName);
            });

        pub.MapGet("/policies",
            async (IMediator m, CancellationToken ct) =>
                Results.Ok(await m.Send(new PolicyFeatures.GetActivePoliciesQuery(), ct)));

        // Guardian-side surface for the parental consent ceremony. Both
        // the URL (id + token) come from the email — rate limiting against
        // brute force is pushed up to the reverse proxy.
        pub.MapGet("/parental-consent/{id:guid}/{token}",
            async (Guid id, string token, IMediator m, CancellationToken ct) =>
            {
                var task = await m.Send(new ParentalConsentFeatures.GetGuardianTaskQuery(id, token), ct);
                return task is null ? Results.NotFound() : Results.Ok(task);
            });

        pub.MapPost("/parental-consent/{id:guid}/{token}",
            async (Guid id, string token, GuardianDecisionRequest body, IMediator m, HttpContext http, CancellationToken ct) =>
            {
                try
                {
                    var result = await m.Send(new ParentalConsentFeatures.DecideParentalConsentCommand(
                        id, token, body.Approve, body.DeclineReason,
                        http.Connection.RemoteIpAddress?.ToString(),
                        http.Request.Headers.UserAgent.ToString()), ct);
                    return Results.Ok(result);
                }
                catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
                catch (KeyNotFoundException) { return Results.NotFound(); }
                catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
            });
    }

    // ──────────────────────────────────────── member surface
    private static void MapMember(IEndpointRouteBuilder app)
    {
        var mem = app.MapGroup("/api/policies").WithTags("Compliance");

        mem.MapPost("/accept",
            async (AcceptPoliciesRequest body, IMediator m, HttpContext http, CancellationToken ct) =>
            {
                if (body.MemberId == Guid.Empty || body.PolicyDocumentIds is null || body.PolicyDocumentIds.Count == 0)
                    return Results.BadRequest(new { error = "memberId and policyDocumentIds are required." });

                var result = await m.Send(new PolicyFeatures.AcceptPoliciesCommand(
                    body.MemberId, body.PolicyDocumentIds,
                    http.Connection.RemoteIpAddress?.ToString(),
                    http.Request.Headers.UserAgent.ToString()), ct);
                return Results.Ok(result);
            });
    }

    // ──────────────────────────────────────── admin surface
    private static void MapAdmin(IEndpointRouteBuilder app)
    {
        var adm = app.MapGroup("/api/admin").WithTags("Compliance");

        // ── governance docs upload (multipart) ───────────
        adm.MapPost("/governance/documents",
            async ([FromForm] string title,
                   [FromForm] string? description,
                   [FromForm] GovernanceDocumentType documentType,
                   [FromForm] int? coveringYear,
                   IFormFile file,
                   IMediator m, CancellationToken ct) =>
            {
                if (file is null || file.Length == 0) return Results.BadRequest(new { error = "file is required." });
                await using var stream = file.OpenReadStream();
                var dto = await m.Send(new GovernanceFeatures.UploadGovernanceDocumentCommand(
                    title, description, documentType, coveringYear,
                    stream, file.FileName, file.ContentType, UploadedByMemberId: null), ct);
                return Results.Created($"/api/governance/documents/{dto.Id}", dto);
            })
            .DisableAntiforgery();

        adm.MapPut("/governance/documents/{id:guid}",
            async (Guid id, UpdateGovernanceDocumentRequest body, IMediator m, CancellationToken ct) =>
                Results.Ok(await m.Send(new GovernanceFeatures.UpdateGovernanceDocumentCommand(
                    id, body.Title, body.Description, body.DocumentType, body.CoveringYear), ct)));

        adm.MapPost("/governance/documents/{id:guid}/publish",
            async (Guid id, IMediator m, CancellationToken ct) =>
                Results.Ok(await m.Send(new GovernanceFeatures.PublishGovernanceDocumentCommand(id, true), ct)));

        adm.MapPost("/governance/documents/{id:guid}/unpublish",
            async (Guid id, IMediator m, CancellationToken ct) =>
                Results.Ok(await m.Send(new GovernanceFeatures.PublishGovernanceDocumentCommand(id, false), ct)));

        adm.MapDelete("/governance/documents/{id:guid}",
            async (Guid id, IMediator m, CancellationToken ct) =>
            {
                await m.Send(new GovernanceFeatures.DeleteGovernanceDocumentCommand(id), ct);
                return Results.NoContent();
            });

        adm.MapGet("/governance/documents",
            async (IMediator m, GovernanceDocumentType? type, int? year, CancellationToken ct) =>
                Results.Ok(await m.Send(new GovernanceFeatures.ListGovernanceDocumentsQuery(false, type, year), ct)));

        // ── policy versions ──────────────────────────────
        adm.MapPost("/policies",
            async (PublishPolicyRequest body, IMediator m, CancellationToken ct) =>
                Results.Ok(await m.Send(new PolicyFeatures.PublishPolicyVersionCommand(
                    body.Kind, body.Version, body.Title, body.BodyMarkdown,
                    body.EffectiveAtUtc ?? DateTime.UtcNow, body.PublishedByMemberId), ct)));

        adm.MapGet("/policies",
            async (IMediator m, PolicyDocumentKind? kind, CancellationToken ct) =>
                Results.Ok(await m.Send(new PolicyFeatures.ListPolicyVersionsQuery(kind), ct)));

        adm.MapGet("/policies/acceptances",
            async (IMediator m, Guid? memberId, PolicyDocumentKind? kind, int page = 1, int pageSize = 50, CancellationToken ct = default) =>
                Results.Ok(await m.Send(new PolicyFeatures.ListAcceptancesQuery(memberId, kind, page, pageSize), ct)));

        // ── parental consents ────────────────────────────
        adm.MapGet("/parental-consents",
            async (IMediator m, ParentalConsentStatus? status, int page = 1, int pageSize = 50, CancellationToken ct = default) =>
                Results.Ok(await m.Send(new ParentalConsentFeatures.ListConsentsQuery(status, page, pageSize), ct)));

        adm.MapGet("/consent-logs",
            async (IMediator m, Guid? memberId, Guid? consentId, int page = 1, int pageSize = 100, CancellationToken ct = default) =>
                Results.Ok(await m.Send(new ParentalConsentFeatures.ListConsentLogsQuery(memberId, consentId, page, pageSize), ct)));
    }
}

// ──────────────── request DTOs (kept inline for brevity) ──────────────
public sealed record GuardianDecisionRequest(bool Approve, string? DeclineReason);

public sealed record AcceptPoliciesRequest(Guid MemberId, List<Guid> PolicyDocumentIds);

public sealed record UpdateGovernanceDocumentRequest(
    string Title, string? Description, GovernanceDocumentType DocumentType, int? CoveringYear);

public sealed record PublishPolicyRequest(
    PolicyDocumentKind Kind,
    string Version,
    string Title,
    string BodyMarkdown,
    DateTime? EffectiveAtUtc,
    Guid? PublishedByMemberId);
