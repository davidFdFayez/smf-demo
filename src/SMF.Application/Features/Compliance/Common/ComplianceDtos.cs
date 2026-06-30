using SMF.Domain.Enums;

namespace SMF.Application.Features.Compliance.Common;

public sealed record GovernanceDocumentDto(
    Guid Id,
    string Title,
    string? Description,
    GovernanceDocumentType DocumentType,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    string Sha256,
    int? CoveringYear,
    bool IsPublished,
    DateTime? PublishedAtUtc,
    DateTime UploadedAtUtc,
    string DownloadUrl);

public sealed record PolicyDocumentDto(
    Guid Id,
    PolicyDocumentKind Kind,
    string Version,
    string Title,
    string BodyMarkdown,
    string ContentHash,
    bool IsActive,
    DateTime EffectiveAtUtc,
    DateTime CreatedAtUtc);

public sealed record PolicyAcceptanceDto(
    Guid Id,
    Guid MemberId,
    string MemberName,
    PolicyDocumentKind PolicyKind,
    string PolicyVersion,
    string ContentHash,
    DateTime AcceptedAtUtc,
    string? IpAddress);

public sealed record ParentalConsentDto(
    Guid Id,
    Guid MemberId,
    string MemberName,
    ParentalConsentStatus Status,
    string GuardianFullName,
    GuardianRelation Relation,
    string GuardianEmail,
    string GuardianPhone,
    DateTime CreatedAtUtc,
    DateTime TokenExpiresAtUtc,
    DateTime? OpenedAtUtc,
    DateTime? DecidedAtUtc,
    string? DeclineReason);

public sealed record ConsentLogDto(
    long Id,
    DateTime OccurredAtUtc,
    ConsentEventType EventType,
    Guid? MemberId,
    Guid? PolicyDocumentId,
    Guid? ParentalConsentId,
    string? PolicyKind,
    string? PolicyVersion,
    string? IpAddress,
    string? DetailsJson);

public sealed record GuardianTaskDto(
    Guid ConsentId,
    string MemberName,
    DateOnly MemberDateOfBirth,
    string GuardianFullName,
    GuardianRelation Relation,
    DateTime TokenExpiresAtUtc,
    PolicyDocumentDto? Policy,
    bool IsExpired,
    bool AlreadyDecided);
