using MediatR;
using Microsoft.AspNetCore.Mvc;
using SMF.Application.Features.ELearning;
using SMF.Domain.Enums;

namespace SMF.Api.Endpoints;

public static class ELearningEndpoints
{
    public static IEndpointRouteBuilder MapELearningEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/courses").WithTags("E-Learning");

        // Public catalog — published, non-archived.
        group.MapGet("/", async (
                [FromQuery] CourseCategory? category,
                [FromQuery] CourseLevel? level,
                ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListPublishedCoursesQuery(category, level), ct)))
            .WithName("ListPublishedCourses");

        // Admin view — all courses including drafts + archived.
        group.MapGet("/admin", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListAllCoursesQuery(), ct)))
            .WithName("ListAllCourses");

        group.MapGet("/{slug}", async (string slug, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetCourseBySlugQuery(slug), ct)))
            .WithName("GetCourseBySlug")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{courseId:guid}/lessons/{lessonId:guid}", async (
                Guid courseId, Guid lessonId, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetLessonQuery(courseId, lessonId), ct)))
            .WithName("GetLesson");

        group.MapPost("/", async (
                [FromBody] CreateCourseCommand cmd, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(cmd, ct);
            return Results.Created($"/api/courses/{result.Slug}", result);
        }).WithName("CreateCourse").ProducesValidationProblem();

        group.MapPost("/{id:guid}/lessons", async (
                Guid id, [FromBody] AddLessonCommandBody body,
                ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new AddLessonCommand(
                id, body.Title, body.Summary, body.Content, body.VideoUrl, body.EstimatedMinutes), ct)))
            .WithName("AddLesson")
            .ProducesValidationProblem();

        group.MapPost("/{id:guid}/publish", async (Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new PublishCourseCommand(id), ct)))
            .WithName("PublishCourse");

        group.MapPost("/{id:guid}/archive", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ArchiveCourseCommand(id), ct);
            return Results.NoContent();
        }).WithName("ArchiveCourse");

        // Enrollment / progress
        group.MapPost("/{courseId:guid}/enroll", async (
                Guid courseId, [FromBody] EnrollBody body,
                ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new EnrollInCourseCommand(courseId, body.MemberId), ct)))
            .WithName("EnrollInCourse");

        group.MapPost("/enrollments/{enrollmentId:guid}/lessons/{lessonId:guid}/complete", async (
                Guid enrollmentId, Guid lessonId, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new CompleteLessonCommand(enrollmentId, lessonId), ct)))
            .WithName("CompleteLesson");

        group.MapGet("/{courseId:guid}/enrollments/by-member/{memberId:guid}", async (
                Guid courseId, Guid memberId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetEnrollmentQuery(courseId, memberId), ct);
            return result is null ? Results.NoContent() : Results.Ok(result);
        }).WithName("GetEnrollment");

        return app;
    }

    // Body types kept on the endpoint surface so the command doesn't leak the
    // path-parameter id — the handler receives the merged command.
    public sealed record AddLessonCommandBody(
        string Title, string Summary, string Content,
        string? VideoUrl, int EstimatedMinutes);

    public sealed record EnrollBody(Guid MemberId);
}
