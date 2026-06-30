using System.Runtime.CompilerServices;
using System.Text;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Interfaces;
using SMF.Application.Features.Communication.Broadcasts;
using SMF.Application.Features.Communication.Common;
using SMF.Application.Features.Communication.Devices;
using SMF.Application.Features.Communication.Feedback;
using SMF.Application.Features.Communication.Social;
using SMF.Application.Features.Members.Queries.GetMembers;
using SMF.Domain.Enums;

namespace SMF.Api.Endpoints;

/// <summary>
/// Communication & engagement HTTP surface:
///
///   * Public:
///       <c>POST /api/feedback</c>            — submit rating/comment (members + guests).
///       <c>GET  /api/feedback</c>            — public list of approved feedback.
///       <c>GET  /api/feedback/stats</c>      — aggregated counters.
///       <c>GET  /api/social/highlights</c>   — published curated social posts.
///       <c>POST /api/devices/register</c>    — upsert FCM device token.
///       <c>POST /api/devices/unregister</c>  — explicit "log out".
///       <c>POST /api/chatbot/stream</c>      — SSE FAQ assistant stream.
///   * Admin:
///       <c>POST   /api/admin/broadcasts</c>          — create campaign.
///       <c>GET    /api/admin/broadcasts</c>          — list campaigns.
///       <c>GET    /api/admin/broadcasts/{id}</c>     — campaign detail.
///       <c>POST   /api/admin/broadcasts/{id}/send</c>— execute now.
///       <c>POST   /api/admin/broadcasts/{id}/cancel</c>
///       <c>POST   /api/admin/broadcasts/preview</c>  — audience size preview.
///       <c>GET    /api/admin/feedback</c>            — moderation list.
///       <c>POST   /api/admin/feedback/{id}/moderate</c>
///       <c>POST   /api/admin/social/highlights</c>   — CRUD.
///       <c>PUT    /api/admin/social/highlights/{id}</c>
///       <c>DELETE /api/admin/social/highlights/{id}</c>
/// </summary>
public static class CommunicationEndpoints
{
    public static IEndpointRouteBuilder MapCommunicationEndpoints(this IEndpointRouteBuilder app)
    {
        MapPublic(app);
        MapAdmin(app);
        return app;
    }

    // ───────────────────────────────────────── public surface

    private static void MapPublic(IEndpointRouteBuilder app)
    {
        var feedback = app.MapGroup("/api/feedback").WithTags("Feedback");
        feedback.MapPost("/", async ([FromBody] SubmitFeedbackRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SubmitFeedbackCommand(
                body.SubjectType, body.SubjectId, body.Rating, body.Comment,
                body.AuthorMemberId, body.AuthorName, body.AuthorEmail), ct);
            return Results.Created($"/api/feedback/{result.Id}", result);
        }).WithName("SubmitFeedback").ProducesValidationProblem();

        feedback.MapGet("/", async (
                ISender sender, CancellationToken ct,
                [FromQuery] FeedbackSubjectType? subjectType = null,
                [FromQuery] Guid? subjectId = null,
                [FromQuery] int? minRating = null,
                [FromQuery] int page = 1,
                [FromQuery] int pageSize = 20) =>
            // Public listing only ever exposes Public-state rows.
            Results.Ok(await sender.Send(
                new ListFeedbackQuery(page, pageSize, subjectType, subjectId, FeedbackStatus.Public, minRating), ct))
        ).WithName("ListPublicFeedback");

        feedback.MapGet("/stats", async (
                ISender sender, CancellationToken ct,
                [FromQuery] FeedbackSubjectType? subjectType = null,
                [FromQuery] Guid? subjectId = null) =>
            Results.Ok(await sender.Send(new FeedbackStatsQuery(subjectType, subjectId), ct))
        ).WithName("FeedbackStats");

        var social = app.MapGroup("/api/social").WithTags("Social");
        social.MapGet("/highlights", async (
                ISender sender, CancellationToken ct,
                [FromQuery] SocialPlatform? platform = null,
                [FromQuery] int? take = null) =>
            Results.Ok(await sender.Send(
                new ListSocialHighlightsQuery(IncludeUnpublished: false, platform, take), ct))
        ).WithName("ListSocialHighlights");

        var devices = app.MapGroup("/api/devices").WithTags("Devices");
        devices.MapPost("/register", async ([FromBody] RegisterDeviceCommand cmd, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(cmd, ct))
        ).WithName("RegisterDevice").ProducesValidationProblem();

        devices.MapPost("/unregister", async ([FromBody] UnregisterDeviceCommand cmd, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(cmd, ct);
            return Results.NoContent();
        }).WithName("UnregisterDevice").ProducesValidationProblem();

        var chatbot = app.MapGroup("/api/chatbot").WithTags("Chatbot");
        chatbot.MapGet("/status", (IChatbotService bot) =>
            Results.Ok(new { configured = bot.IsConfigured })
        ).WithName("ChatbotStatus");

        chatbot.MapPost("/stream", StreamChatReplyAsync)
            .WithName("ChatbotStream")
            .ProducesValidationProblem();
    }

    private static async Task StreamChatReplyAsync(
        HttpContext http,
        [FromBody] ChatStreamRequest body,
        IChatbotService bot,
        IApplicationDbContext db,
        IDateTimeProvider clock,
        CancellationToken ct)
    {
        if (body?.Messages is null || body.Messages.Count == 0)
        {
            http.Response.StatusCode = StatusCodes.Status400BadRequest;
            await http.Response.WriteAsJsonAsync(new { error = "messages array required" }, ct);
            return;
        }

        // Persist a transcript so admins can audit usage. We accept either a
        // member id (authenticated app) or a guest key (browser localStorage)
        // — the entity invariant enforces "exactly one of the two".
        Domain.Entities.ChatConversation? convo = null;
        if (body.MemberId is { } mid && mid != Guid.Empty)
        {
            convo = Domain.Entities.ChatConversation.Start(mid, null, body.Title ?? "FAQ chat", clock.UtcNow);
        }
        else if (body.GuestKey is { } gk && gk != Guid.Empty)
        {
            convo = Domain.Entities.ChatConversation.Start(null, gk, body.Title ?? "FAQ chat", clock.UtcNow);
        }

        if (convo is not null)
        {
            // Persist the user turn(s) before we start streaming so a network
            // hiccup mid-stream doesn't lose them.
            foreach (var m in body.Messages)
            {
                if (string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
                    convo.AddMessage(ChatMessageRole.User, m.Content, clock.UtcNow);
            }
            db.ChatConversations.Add(convo);
            await db.SaveChangesAsync(ct);
        }

        // SSE response: declare ahead of streaming so the browser flushes
        // tokens as they arrive instead of buffering until completion.
        http.Response.Headers.ContentType = "text/event-stream";
        http.Response.Headers.CacheControl = "no-cache";
        http.Response.Headers["X-Accel-Buffering"] = "no";

        var assistantBuffer = new StringBuilder();
        var history = body.Messages.Select(m => new ChatTurn(m.Role, m.Content)).ToList();

        try
        {
            await foreach (var chunk in bot.StreamReplyAsync(history, ct))
            {
                assistantBuffer.Append(chunk);
                await http.Response.WriteAsync($"data: {EscapeSse(chunk)}\n\n", ct);
                await http.Response.Body.FlushAsync(ct);
            }
            await http.Response.WriteAsync("data: [DONE]\n\n", ct);
        }
        catch (OperationCanceledException) { /* client closed the connection */ }
        finally
        {
            if (convo is not null && assistantBuffer.Length > 0)
            {
                convo.AddMessage(ChatMessageRole.Assistant, assistantBuffer.ToString(), clock.UtcNow);
                await db.SaveChangesAsync(CancellationToken.None);
            }
        }
    }

    // ───────────────────────────────────────── admin surface

    private static void MapAdmin(IEndpointRouteBuilder app)
    {
        var broadcasts = app.MapGroup("/api/admin/broadcasts").WithTags("AdminBroadcasts");

        broadcasts.MapPost("/", async ([FromBody] CreateBroadcastCommand cmd, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(cmd, ct);
            return Results.Created($"/api/admin/broadcasts/{id}", new { id });
        }).WithName("CreateBroadcast").ProducesValidationProblem();

        broadcasts.MapGet("/", async (
                ISender sender, CancellationToken ct,
                [FromQuery] BroadcastStatus? status = null,
                [FromQuery] string? search = null,
                [FromQuery] int page = 1,
                [FromQuery] int pageSize = 25) =>
            Results.Ok(await sender.Send(new ListBroadcastsQuery(page, pageSize, status, search), ct))
        ).WithName("ListBroadcasts");

        broadcasts.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetBroadcastQuery(id), ct))
        ).WithName("GetBroadcast");

        broadcasts.MapPost("/{id:guid}/send", async (Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new SendBroadcastCommand(id), ct))
        ).WithName("SendBroadcast");

        broadcasts.MapPost("/{id:guid}/cancel", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new CancelBroadcastCommand(id), ct);
            return Results.NoContent();
        }).WithName("CancelBroadcast");

        broadcasts.MapPost("/preview", async ([FromBody] PreviewBroadcastAudienceQuery q, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(q, ct))
        ).WithName("PreviewBroadcastAudience");

        // Admin notifications log: read-only audit view of recent sends.
        var notifications = app.MapGroup("/api/admin/notifications").WithTags("AdminNotifications");
        notifications.MapGet("/", async (
                IApplicationDbContext db,
                CancellationToken ct,
                [FromQuery] NotificationStatus? status = null,
                [FromQuery] NotificationChannel? channel = null,
                [FromQuery] Guid? campaignId = null,
                [FromQuery] int page = 1,
                [FromQuery] int pageSize = 25) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var q = db.Notifications.AsNoTracking().AsQueryable();
            if (status is { } s) q = q.Where(n => n.Status == s);
            if (channel is { } c) q = q.Where(n => n.Channel == c);
            if (campaignId is { } cid) q = q.Where(n => n.BroadcastCampaignId == cid);

            var total = await q.CountAsync(ct);
            var items = await q.OrderByDescending(n => n.CreatedAtUtc)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(n => new NotificationDto(
                    n.Id, n.Channel, n.RecipientAddress, n.Subject, n.Body,
                    n.Status, n.AttemptCount, n.LastError, n.ProviderMessageId,
                    n.MemberId, n.BroadcastCampaignId, n.CreatedAtUtc, n.SentAtUtc))
                .ToListAsync(ct);

            return Results.Ok(new PagedResult<NotificationDto>(items, total, page, pageSize));
        }).WithName("ListNotifications");

        // Admin feedback moderation.
        var feedback = app.MapGroup("/api/admin/feedback").WithTags("AdminFeedback");
        feedback.MapGet("/", async (
                ISender sender, CancellationToken ct,
                [FromQuery] FeedbackStatus? status = null,
                [FromQuery] FeedbackSubjectType? subjectType = null,
                [FromQuery] Guid? subjectId = null,
                [FromQuery] int? minRating = null,
                [FromQuery] int page = 1,
                [FromQuery] int pageSize = 25) =>
            Results.Ok(await sender.Send(
                new ListFeedbackQuery(page, pageSize, subjectType, subjectId, status, minRating), ct))
        ).WithName("AdminListFeedback");

        feedback.MapPost("/{id:guid}/moderate", async (
                Guid id,
                [FromBody] ModerateFeedbackRequest body,
                ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ModerateFeedbackCommand(
                id, body.NewStatus, body.ModeratorMemberId, body.AdminNotes), ct))
        ).WithName("ModerateFeedback").ProducesValidationProblem();

        // Admin social highlights CRUD.
        var social = app.MapGroup("/api/admin/social/highlights").WithTags("AdminSocial");
        social.MapGet("/", async (
                ISender sender, CancellationToken ct,
                [FromQuery] SocialPlatform? platform = null) =>
            Results.Ok(await sender.Send(
                new ListSocialHighlightsQuery(IncludeUnpublished: true, platform, null), ct))
        ).WithName("AdminListSocialHighlights");

        social.MapPost("/", async ([FromBody] CreateSocialHighlightCommand cmd, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(cmd, ct);
            return Results.Created($"/api/admin/social/highlights/{id}", new { id });
        }).WithName("CreateSocialHighlight").ProducesValidationProblem();

        social.MapPut("/{id:guid}", async (Guid id, [FromBody] UpdateSocialHighlightCommand body, ISender sender, CancellationToken ct) =>
        {
            if (body.Id != id)
                return Results.BadRequest(new { error = "Mismatched id." });
            await sender.Send(body, ct);
            return Results.NoContent();
        }).WithName("UpdateSocialHighlight").ProducesValidationProblem();

        social.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteSocialHighlightCommand(id), ct);
            return Results.NoContent();
        }).WithName("DeleteSocialHighlight");
    }

    // ── helpers / DTOs ───────────────────────────────────────────────

    /// <summary>Encode a chunk for SSE: collapse line breaks to a single
    /// literal so the multi-line "data:" framing stays well-formed.</summary>
    private static string EscapeSse(string chunk)
        => chunk.Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\r", "\\n");

    public sealed record SubmitFeedbackRequest(
        FeedbackSubjectType SubjectType,
        Guid? SubjectId,
        int Rating,
        string Comment,
        Guid? AuthorMemberId,
        string AuthorName,
        string? AuthorEmail);

    public sealed record ModerateFeedbackRequest(
        FeedbackStatus NewStatus,
        Guid? ModeratorMemberId,
        string? AdminNotes);

    public sealed record ChatStreamRequest(
        Guid? MemberId,
        Guid? GuestKey,
        string? Title,
        IReadOnlyList<ChatStreamMessage> Messages);

    public sealed record ChatStreamMessage(string Role, string Content);
}
