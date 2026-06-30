using MediatR;
using Microsoft.AspNetCore.Mvc;
using SMF.Application.Features.News;
using SMF.Domain.Enums;

namespace SMF.Api.Endpoints;

public static class NewsEndpoints
{
    public static IEndpointRouteBuilder MapNewsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/news").WithTags("News");

        // Public homepage feed — published, non-archived only.
        group.MapGet("/", async (
                [FromQuery] NewsCategory? category,
                [FromQuery] int? limit,
                ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(
                new ListPublishedNewsQuery(category, limit is null or <= 0 ? 20 : limit.Value), ct)))
            .WithName("ListPublishedNews");

        // Admin view — all articles including drafts and archived.
        group.MapGet("/admin", async ([FromQuery] int? limit, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(
                new ListAllNewsQuery(limit is null or <= 0 ? 100 : limit.Value), ct)))
            .WithName("ListAllNews");

        group.MapGet("/{slug}", async (string slug, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetNewsBySlugQuery(slug), ct)))
            .WithName("GetNewsBySlug")
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Admin lookup — opts in to drafts / archived so the content editor
        // can still view unpublished articles.
        group.MapGet("/admin/{slug}", async (string slug, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetNewsBySlugQuery(slug, IncludeUnpublished: true), ct)))
            .WithName("GetNewsBySlugAdmin")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
                [FromBody] CreateNewsArticleCommand cmd,
                ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(cmd, ct);
            return Results.Created($"/api/news/{result.Slug}", result);
        })
            .WithName("CreateNewsArticle")
            .ProducesValidationProblem();

        group.MapPut("/{id:guid}", async (
                Guid id,
                [FromBody] UpdateNewsArticleBody body,
                ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new UpdateNewsArticleCommand(
                id, body.Title, body.Summary, body.Body, body.Category, body.CoverImageUrl), ct);
            return Results.Ok(result);
        }).WithName("UpdateNewsArticle").ProducesValidationProblem();

        group.MapPost("/{id:guid}/publish", async (Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new PublishNewsArticleCommand(id), ct)))
            .WithName("PublishNewsArticle");

        group.MapPost("/{id:guid}/archive", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ArchiveNewsArticleCommand(id), ct);
            return Results.NoContent();
        }).WithName("ArchiveNewsArticle");

        return app;
    }

    public sealed record UpdateNewsArticleBody(
        string Title,
        string Summary,
        string Body,
        NewsCategory Category,
        string? CoverImageUrl);
}
