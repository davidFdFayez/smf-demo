using SMF.Domain.Enums;

namespace SMF.Domain.Entities;

/// <summary>
/// A confidential report submitted to the federation's safeguarding officer
/// (PDF §8 "Anti-Doping &amp; Safeguarding"). Reports may be anonymous — the
/// reporter identity fields are optional.
/// </summary>
public class SafeguardingReport
{
    public Guid Id { get; private set; }
    public string ReferenceCode { get; private set; } = default!;
    public SafeguardingCategory Category { get; private set; }
    public string Subject { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public string? IncidentLocation { get; private set; }
    public DateOnly? IncidentDate { get; private set; }
    public bool IsAnonymous { get; private set; }
    public string? ReporterName { get; private set; }
    public string? ReporterEmail { get; private set; }
    public string? ReporterPhone { get; private set; }
    public SafeguardingReportStatus Status { get; private set; }
    public string? ReviewerNotes { get; private set; }
    public DateTime SubmittedAtUtc { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }

    private SafeguardingReport() { }

    public static SafeguardingReport Submit(
        SafeguardingCategory category,
        string subject,
        string description,
        string? incidentLocation,
        DateOnly? incidentDate,
        bool isAnonymous,
        string? reporterName,
        string? reporterEmail,
        string? reporterPhone)
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Subject is required.", nameof(subject));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));

        // If the reporter opted in to being contactable, an email OR phone is
        // required so the safeguarding officer can follow up. Anonymous
        // reports skip this entirely.
        if (!isAnonymous
            && string.IsNullOrWhiteSpace(reporterEmail)
            && string.IsNullOrWhiteSpace(reporterPhone))
        {
            throw new ArgumentException(
                "Non-anonymous reports must include at least an email or phone number.");
        }

        var report = new SafeguardingReport
        {
            Id = Guid.NewGuid(),
            ReferenceCode = BuildReferenceCode(),
            Category = category,
            Subject = subject.Trim(),
            Description = description.Trim(),
            IncidentLocation = string.IsNullOrWhiteSpace(incidentLocation) ? null : incidentLocation.Trim(),
            IncidentDate = incidentDate,
            IsAnonymous = isAnonymous,
            ReporterName = isAnonymous ? null : reporterName?.Trim(),
            ReporterEmail = isAnonymous ? null : reporterEmail?.Trim(),
            ReporterPhone = isAnonymous ? null : reporterPhone?.Trim(),
            Status = SafeguardingReportStatus.Submitted,
            SubmittedAtUtc = DateTime.UtcNow,
        };
        return report;
    }

    public void MarkUnderReview(string? reviewerNotes)
    {
        Status = SafeguardingReportStatus.UnderReview;
        if (!string.IsNullOrWhiteSpace(reviewerNotes))
            ReviewerNotes = reviewerNotes.Trim();
    }

    public void Resolve(string? reviewerNotes, bool dismissed)
    {
        Status = dismissed
            ? SafeguardingReportStatus.Dismissed
            : SafeguardingReportStatus.Resolved;
        if (!string.IsNullOrWhiteSpace(reviewerNotes))
            ReviewerNotes = reviewerNotes.Trim();
        ResolvedAtUtc = DateTime.UtcNow;
    }

    // SG-YYYY-XXXXXX — a short, case-insensitive code the reporter can use
    // to later check status without needing to identify themselves.
    private static string BuildReferenceCode()
    {
        var year = DateTime.UtcNow.Year;
        var tail = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        return $"SG-{year}-{tail}";
    }
}
