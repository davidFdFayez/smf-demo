using SMF.Domain.Enums;

namespace SMF.Domain.Entities;

/// <summary>
/// SOPC-required transparency document published by the federation: annual
/// reports, anti-doping policies, athlete-protection rules, statutes etc.
/// Files themselves live in object storage (or local disk in dev) — this
/// entity holds the metadata + opaque storage key the file storage adapter
/// resolves into a real download URL.
/// </summary>
public class GovernanceDocument
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = default!;
    public string? Description { get; private set; }
    public GovernanceDocumentType DocumentType { get; private set; }

    /// <summary>
    /// Storage adapter key (e.g. <c>governance/2026/annual-report.pdf</c>).
    /// Internal — never returned directly to clients; resolved into a
    /// signed URL by <c>IFileStorage</c>.
    /// </summary>
    public string StorageKey { get; private set; } = default!;
    public string OriginalFileName { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public long FileSizeBytes { get; private set; }

    /// <summary>SHA-256 of the file as uploaded — proves the bytes haven't
    /// been silently rewritten in storage. Surfaced on the public detail
    /// page for stakeholders who want to verify integrity offline.</summary>
    public string Sha256 { get; private set; } = default!;

    /// <summary>Year the document covers (e.g. 2025 for an Annual Report).
    /// Optional because some documents (statutes, policies) aren't tied to
    /// a specific year.</summary>
    public int? CoveringYear { get; private set; }

    public bool IsPublished { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public DateTime UploadedAtUtc { get; private set; }
    public Guid? UploadedByMemberId { get; private set; }

    private GovernanceDocument() { }

    public static GovernanceDocument Upload(
        string title,
        string? description,
        GovernanceDocumentType type,
        string storageKey,
        string originalFileName,
        string contentType,
        long fileSizeBytes,
        string sha256,
        int? coveringYear,
        Guid? uploadedByMemberId)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ArgumentException("Storage key is required.", nameof(storageKey));
        if (string.IsNullOrWhiteSpace(originalFileName))
            throw new ArgumentException("Original file name is required.", nameof(originalFileName));
        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Content type is required.", nameof(contentType));
        if (string.IsNullOrWhiteSpace(sha256) || sha256.Length != 64)
            throw new ArgumentException("SHA-256 must be 64 hex chars.", nameof(sha256));
        if (fileSizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(fileSizeBytes));
        if (coveringYear is < 1900 or > 2200)
            throw new ArgumentOutOfRangeException(nameof(coveringYear));

        return new GovernanceDocument
        {
            Id = Guid.NewGuid(),
            Title = title.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            DocumentType = type,
            StorageKey = storageKey,
            OriginalFileName = originalFileName,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            Sha256 = sha256.ToLowerInvariant(),
            CoveringYear = coveringYear,
            UploadedAtUtc = DateTime.UtcNow,
            UploadedByMemberId = uploadedByMemberId,
            IsPublished = false
        };
    }

    public void UpdateMetadata(string title, string? description, GovernanceDocumentType type, int? coveringYear)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (coveringYear is < 1900 or > 2200)
            throw new ArgumentOutOfRangeException(nameof(coveringYear));

        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DocumentType = type;
        CoveringYear = coveringYear;
    }

    public void Publish()
    {
        if (IsPublished) return;
        IsPublished = true;
        PublishedAtUtc = DateTime.UtcNow;
    }

    public void Unpublish()
    {
        IsPublished = false;
        PublishedAtUtc = null;
    }
}
