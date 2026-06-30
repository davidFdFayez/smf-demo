using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Entities;
using SMF.Domain.Enums;

namespace SMF.Application.Features.ELearning;

// ─── DTOs ───────────────────────────────────────────────────────────────────

public sealed record LessonSummary(
    Guid Id,
    int Order,
    string Title,
    string Summary,
    int EstimatedMinutes,
    bool HasVideo);

public sealed record LessonDetails(
    Guid Id,
    Guid CourseId,
    int Order,
    string Title,
    string Summary,
    string Content,
    string? VideoUrl,
    int EstimatedMinutes);

public sealed record CourseSummary(
    Guid Id,
    string Slug,
    string Title,
    string Summary,
    CourseCategory Category,
    CourseLevel Level,
    string InstructorName,
    string? CoverImageUrl,
    int LessonCount,
    int TotalMinutes,
    int EnrollmentCount,
    bool IsPublished,
    bool IsArchived,
    DateTime CreatedAtUtc,
    DateTime? PublishedAtUtc);

public sealed record CourseDetails(
    Guid Id,
    string Slug,
    string Title,
    string Summary,
    CourseCategory Category,
    CourseLevel Level,
    string InstructorName,
    string? CoverImageUrl,
    bool IsPublished,
    bool IsArchived,
    DateTime CreatedAtUtc,
    DateTime? PublishedAtUtc,
    IReadOnlyList<LessonSummary> Lessons,
    int TotalMinutes,
    int EnrollmentCount);

public sealed record EnrollmentProgress(
    Guid EnrollmentId,
    Guid CourseId,
    Guid MemberId,
    CourseEnrollmentStatus Status,
    int LessonsCompleted,
    int LessonsTotal,
    double ProgressPercent,
    DateTime EnrolledAtUtc,
    DateTime? CompletedAtUtc);

// ─── Commands: Author course / lessons ──────────────────────────────────────

public sealed record CreateCourseCommand(
    string Title,
    string Summary,
    CourseCategory Category,
    CourseLevel Level,
    string InstructorName,
    string? CoverImageUrl) : IRequest<CourseDetails>;

public sealed class CreateCourseCommandValidator : AbstractValidator<CreateCourseCommand>
{
    public CreateCourseCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Summary).NotEmpty().MaximumLength(800);
        RuleFor(x => x.InstructorName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.CoverImageUrl).MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.CoverImageUrl));
    }
}

public sealed class CreateCourseCommandHandler : IRequestHandler<CreateCourseCommand, CourseDetails>
{
    private readonly IApplicationDbContext _db;
    public CreateCourseCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CourseDetails> Handle(CreateCourseCommand request, CancellationToken ct)
    {
        var course = Course.Draft(
            request.Title, request.Summary, request.Category, request.Level,
            request.InstructorName, request.CoverImageUrl, DateTime.UtcNow);

        // Slug collision guard, mirroring the news/club pattern.
        var baseSlug = course.Slug;
        var suffix = 1;
        while (await _db.Courses.AnyAsync(c => c.Slug == course.Slug, ct))
        {
            course.ApplySlugSuffix(suffix++);
            if (suffix > 500)
                throw new InvalidOperationException($"Unable to allocate slug for '{baseSlug}'.");
        }

        _db.Courses.Add(course);
        await _db.SaveChangesAsync(ct);
        return ELearningMapper.ToDetails(course, enrollmentCount: 0);
    }
}

public sealed record AddLessonCommand(
    Guid CourseId,
    string Title,
    string Summary,
    string Content,
    string? VideoUrl,
    int EstimatedMinutes) : IRequest<LessonDetails>;

public sealed class AddLessonCommandValidator : AbstractValidator<AddLessonCommand>
{
    public AddLessonCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Summary).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Content).NotEmpty().MaximumLength(20_000);
        RuleFor(x => x.EstimatedMinutes).InclusiveBetween(0, 600);
        RuleFor(x => x.VideoUrl).MaximumLength(500);
    }
}

public sealed class AddLessonCommandHandler : IRequestHandler<AddLessonCommand, LessonDetails>
{
    private readonly IApplicationDbContext _db;
    public AddLessonCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<LessonDetails> Handle(AddLessonCommand request, CancellationToken ct)
    {
        var course = await _db.Courses
                         .Include(c => c.Lessons)
                         .FirstOrDefaultAsync(c => c.Id == request.CourseId, ct)
                     ?? throw new NotFoundException(nameof(Course), request.CourseId);

        var lesson = course.AddLesson(
            request.Title, request.Summary, request.Content,
            request.VideoUrl, request.EstimatedMinutes, DateTime.UtcNow);

        await _db.SaveChangesAsync(ct);
        return new LessonDetails(
            lesson.Id, lesson.CourseId, lesson.Order, lesson.Title,
            lesson.Summary, lesson.Content, lesson.VideoUrl, lesson.EstimatedMinutes);
    }
}

public sealed record PublishCourseCommand(Guid Id) : IRequest<CourseDetails>;

public sealed class PublishCourseCommandHandler : IRequestHandler<PublishCourseCommand, CourseDetails>
{
    private readonly IApplicationDbContext _db;
    public PublishCourseCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CourseDetails> Handle(PublishCourseCommand request, CancellationToken ct)
    {
        var course = await _db.Courses.Include(c => c.Lessons)
                         .FirstOrDefaultAsync(c => c.Id == request.Id, ct)
                     ?? throw new NotFoundException(nameof(Course), request.Id);

        course.Publish();
        await _db.SaveChangesAsync(ct);
        var enrolled = await _db.CourseEnrollments.CountAsync(e => e.CourseId == course.Id, ct);
        return ELearningMapper.ToDetails(course, enrolled);
    }
}

public sealed record ArchiveCourseCommand(Guid Id) : IRequest<Unit>;

public sealed class ArchiveCourseCommandHandler : IRequestHandler<ArchiveCourseCommand, Unit>
{
    private readonly IApplicationDbContext _db;
    public ArchiveCourseCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Unit> Handle(ArchiveCourseCommand request, CancellationToken ct)
    {
        var course = await _db.Courses.FirstOrDefaultAsync(c => c.Id == request.Id, ct)
                     ?? throw new NotFoundException(nameof(Course), request.Id);
        course.Archive();
        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ─── Commands: Enroll / progress ────────────────────────────────────────────

public sealed record EnrollInCourseCommand(Guid CourseId, Guid MemberId) : IRequest<EnrollmentProgress>;

public sealed class EnrollInCourseCommandValidator : AbstractValidator<EnrollInCourseCommand>
{
    public EnrollInCourseCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.MemberId).NotEmpty();
    }
}

public sealed class EnrollInCourseCommandHandler : IRequestHandler<EnrollInCourseCommand, EnrollmentProgress>
{
    private readonly IApplicationDbContext _db;
    public EnrollInCourseCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<EnrollmentProgress> Handle(EnrollInCourseCommand request, CancellationToken ct)
    {
        var course = await _db.Courses.Include(c => c.Lessons)
                         .FirstOrDefaultAsync(c => c.Id == request.CourseId, ct)
                     ?? throw new NotFoundException(nameof(Course), request.CourseId);

        if (!course.IsPublished || course.IsArchived)
            throw new InvalidOperationException("Course is not open for enrollment.");

        var existing = await _db.CourseEnrollments
            .Include(e => e.Completions)
            .FirstOrDefaultAsync(
                e => e.CourseId == request.CourseId && e.MemberId == request.MemberId, ct);

        if (existing is null)
        {
            existing = CourseEnrollment.Enroll(request.CourseId, request.MemberId, DateTime.UtcNow);
            _db.CourseEnrollments.Add(existing);
            await _db.SaveChangesAsync(ct);
        }

        return ELearningMapper.ToProgress(existing, course.Lessons.Count);
    }
}

public sealed record CompleteLessonCommand(Guid EnrollmentId, Guid LessonId) : IRequest<EnrollmentProgress>;

public sealed class CompleteLessonCommandHandler : IRequestHandler<CompleteLessonCommand, EnrollmentProgress>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly IEmailSender _email;
    private readonly ICertificateVerifyUrlBuilder _urlBuilder;
    private readonly ILogger<CompleteLessonCommandHandler> _logger;

    public CompleteLessonCommandHandler(
        IApplicationDbContext db,
        IDateTimeProvider clock,
        IEmailSender email,
        ICertificateVerifyUrlBuilder urlBuilder,
        ILogger<CompleteLessonCommandHandler> logger)
    {
        _db = db;
        _clock = clock;
        _email = email;
        _urlBuilder = urlBuilder;
        _logger = logger;
    }

    public async Task<EnrollmentProgress> Handle(CompleteLessonCommand request, CancellationToken ct)
    {
        var enrollment = await _db.CourseEnrollments.Include(e => e.Completions)
                             .FirstOrDefaultAsync(e => e.Id == request.EnrollmentId, ct)
                         ?? throw new NotFoundException(nameof(CourseEnrollment), request.EnrollmentId);

        var course = await _db.Courses.Include(c => c.Lessons)
                         .FirstOrDefaultAsync(c => c.Id == enrollment.CourseId, ct)
                     ?? throw new NotFoundException(nameof(Course), enrollment.CourseId);

        if (!course.Lessons.Any(l => l.Id == request.LessonId))
            throw new InvalidOperationException(
                $"Lesson {request.LessonId} does not belong to course {course.Id}.");

        var wasAlreadyComplete = enrollment.Status == CourseEnrollmentStatus.Completed;

        var now = _clock.UtcNow;
        enrollment.MarkLessonComplete(request.LessonId, now);
        enrollment.CompleteIfAllLessonsFinished(course.Lessons.Count, now);

        Certificate? newCertificate = null;
        if (!wasAlreadyComplete && enrollment.Status == CourseEnrollmentStatus.Completed)
        {
            // Auto-issue a course completion certificate the first time the
            // enrollment transitions to Completed. Idempotent because we
            // checked the "was already complete" flag BEFORE the entity call.
            newCertificate = Certificate.Issue(
                enrollment.MemberId,
                CertificateType.CourseCompletion,
                course.Title,
                issuingAuthority: course.InstructorName,
                issuedAtUtc: now,
                expiresAtUtc: null);
            _db.Certificates.Add(newCertificate);
        }

        await _db.SaveChangesAsync(ct);

        // Email-on-completion: best-effort, never blocks the API response.
        // The link points at the public certificate-verify page, which
        // also surfaces the PDF download.
        if (newCertificate is not null)
        {
            await TrySendCompletionEmailAsync(enrollment.MemberId, course, newCertificate, ct);
        }

        return ELearningMapper.ToProgress(enrollment, course.Lessons.Count);
    }

    private async Task TrySendCompletionEmailAsync(
        Guid memberId, Course course, Certificate certificate, CancellationToken ct)
    {
        try
        {
            var member = await _db.Members.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == memberId, ct);
            if (member is null || string.IsNullOrWhiteSpace(member.Email)) return;

            var verifyUrl = _urlBuilder.BuildVerifyUrl(certificate.VerificationCode);
            var subject = $"Your certificate: {course.Title}";
            var body = $"""
                Congratulations {member.FullName},

                You've completed the course "{course.Title}" with the Saudi MuayThai Federation.
                Your certificate of completion has been issued and is available to download here:

                  {verifyUrl}

                Verification code: {certificate.VerificationCode}
                Issued: {certificate.IssuedAtUtc:yyyy-MM-dd}

                This is an automated message. If anything looks wrong, please contact us.
                """;

            var result = await _email.SendAsync(
                new EmailMessage(member.Email, subject, body, IsHtml: false, ToName: member.FullName),
                ct);

            if (!result.Delivered)
            {
                _logger.LogWarning(
                    "Certificate completion email did not deliver for member {MemberId}: {Error}",
                    memberId, result.Error);
            }
        }
        catch (Exception ex)
        {
            // Email is best-effort; surface the error in logs but never
            // fail the lesson-complete request.
            _logger.LogWarning(ex,
                "Failed to send certificate completion email for member {MemberId}.", memberId);
        }
    }
}

// ─── Queries ────────────────────────────────────────────────────────────────

public sealed record ListPublishedCoursesQuery(CourseCategory? Category, CourseLevel? Level)
    : IRequest<IReadOnlyList<CourseSummary>>;

public sealed class ListPublishedCoursesQueryHandler
    : IRequestHandler<ListPublishedCoursesQuery, IReadOnlyList<CourseSummary>>
{
    private readonly IApplicationDbContext _db;
    public ListPublishedCoursesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<CourseSummary>> Handle(
        ListPublishedCoursesQuery request, CancellationToken ct)
    {
        var q = _db.Courses.AsNoTracking()
            .Where(c => c.IsPublished && !c.IsArchived);

        if (request.Category is { } cat) q = q.Where(c => c.Category == cat);
        if (request.Level is { } lvl) q = q.Where(c => c.Level == lvl);

        return await q
            .OrderByDescending(c => c.PublishedAtUtc)
            .Select(c => new CourseSummary(
                c.Id, c.Slug, c.Title, c.Summary, c.Category, c.Level,
                c.InstructorName, c.CoverImageUrl,
                c.Lessons.Count(),
                c.Lessons.Sum(l => (int?)l.EstimatedMinutes) ?? 0,
                _db.CourseEnrollments.Count(e => e.CourseId == c.Id),
                c.IsPublished, c.IsArchived,
                c.CreatedAtUtc, c.PublishedAtUtc))
            .ToListAsync(ct);
    }
}

public sealed record ListAllCoursesQuery() : IRequest<IReadOnlyList<CourseSummary>>;

public sealed class ListAllCoursesQueryHandler
    : IRequestHandler<ListAllCoursesQuery, IReadOnlyList<CourseSummary>>
{
    private readonly IApplicationDbContext _db;
    public ListAllCoursesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<CourseSummary>> Handle(ListAllCoursesQuery request, CancellationToken ct)
    {
        return await _db.Courses.AsNoTracking()
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new CourseSummary(
                c.Id, c.Slug, c.Title, c.Summary, c.Category, c.Level,
                c.InstructorName, c.CoverImageUrl,
                c.Lessons.Count(),
                c.Lessons.Sum(l => (int?)l.EstimatedMinutes) ?? 0,
                _db.CourseEnrollments.Count(e => e.CourseId == c.Id),
                c.IsPublished, c.IsArchived,
                c.CreatedAtUtc, c.PublishedAtUtc))
            .ToListAsync(ct);
    }
}

public sealed record GetCourseBySlugQuery(string Slug) : IRequest<CourseDetails>;

public sealed class GetCourseBySlugQueryHandler : IRequestHandler<GetCourseBySlugQuery, CourseDetails>
{
    private readonly IApplicationDbContext _db;
    public GetCourseBySlugQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<CourseDetails> Handle(GetCourseBySlugQuery request, CancellationToken ct)
    {
        var course = await _db.Courses.AsNoTracking()
                         .Include(c => c.Lessons.OrderBy(l => l.Order))
                         .FirstOrDefaultAsync(c => c.Slug == request.Slug, ct)
                     ?? throw new NotFoundException(nameof(Course), request.Slug);

        var enrolled = await _db.CourseEnrollments
            .CountAsync(e => e.CourseId == course.Id, ct);

        return ELearningMapper.ToDetails(course, enrolled);
    }
}

public sealed record GetLessonQuery(Guid CourseId, Guid LessonId) : IRequest<LessonDetails>;

public sealed class GetLessonQueryHandler : IRequestHandler<GetLessonQuery, LessonDetails>
{
    private readonly IApplicationDbContext _db;
    public GetLessonQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<LessonDetails> Handle(GetLessonQuery request, CancellationToken ct)
    {
        var lesson = await _db.Lessons.AsNoTracking()
                         .FirstOrDefaultAsync(
                             l => l.Id == request.LessonId && l.CourseId == request.CourseId, ct)
                     ?? throw new NotFoundException(nameof(Lesson), request.LessonId);

        return new LessonDetails(
            lesson.Id, lesson.CourseId, lesson.Order, lesson.Title,
            lesson.Summary, lesson.Content, lesson.VideoUrl, lesson.EstimatedMinutes);
    }
}

public sealed record GetEnrollmentQuery(Guid CourseId, Guid MemberId) : IRequest<EnrollmentProgress?>;

public sealed class GetEnrollmentQueryHandler : IRequestHandler<GetEnrollmentQuery, EnrollmentProgress?>
{
    private readonly IApplicationDbContext _db;
    public GetEnrollmentQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<EnrollmentProgress?> Handle(GetEnrollmentQuery request, CancellationToken ct)
    {
        var enrollment = await _db.CourseEnrollments.AsNoTracking()
            .Include(e => e.Completions)
            .FirstOrDefaultAsync(
                e => e.CourseId == request.CourseId && e.MemberId == request.MemberId, ct);

        if (enrollment is null) return null;

        var totalLessons = await _db.Lessons.CountAsync(l => l.CourseId == request.CourseId, ct);
        return ELearningMapper.ToProgress(enrollment, totalLessons);
    }
}

// ─── Mapping helpers ────────────────────────────────────────────────────────

internal static class ELearningMapper
{
    public static CourseDetails ToDetails(Course c, int enrollmentCount)
    {
        var lessons = c.Lessons
            .OrderBy(l => l.Order)
            .Select(l => new LessonSummary(
                l.Id, l.Order, l.Title, l.Summary, l.EstimatedMinutes,
                !string.IsNullOrEmpty(l.VideoUrl)))
            .ToList();

        return new CourseDetails(
            c.Id, c.Slug, c.Title, c.Summary, c.Category, c.Level,
            c.InstructorName, c.CoverImageUrl,
            c.IsPublished, c.IsArchived,
            c.CreatedAtUtc, c.PublishedAtUtc,
            lessons,
            c.Lessons.Sum(l => l.EstimatedMinutes),
            enrollmentCount);
    }

    public static EnrollmentProgress ToProgress(CourseEnrollment e, int totalLessons)
    {
        var done = e.Completions.Count;
        var pct = totalLessons == 0 ? 0 : Math.Round(100.0 * done / totalLessons, 1);
        return new EnrollmentProgress(
            e.Id, e.CourseId, e.MemberId, e.Status,
            done, totalLessons, pct,
            e.EnrolledAtUtc, e.CompletedAtUtc);
    }
}
