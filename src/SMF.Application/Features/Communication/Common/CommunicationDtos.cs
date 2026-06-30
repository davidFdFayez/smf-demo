using SMF.Domain.Enums;

namespace SMF.Application.Features.Communication.Common;

public sealed record NotificationDto(
    Guid Id,
    NotificationChannel Channel,
    string RecipientAddress,
    string Subject,
    string Body,
    NotificationStatus Status,
    int AttemptCount,
    string? LastError,
    string? ProviderMessageId,
    Guid? MemberId,
    Guid? BroadcastCampaignId,
    DateTime CreatedAtUtc,
    DateTime? SentAtUtc);

public sealed record BroadcastSummaryDto(
    Guid Id,
    string Title,
    string Subject,
    BroadcastChannel Channels,
    BroadcastStatus Status,
    int TotalTargets,
    int DeliveredCount,
    int FailedCount,
    DateTime CreatedAtUtc,
    DateTime? ScheduledAtUtc,
    DateTime? CompletedAtUtc);

public sealed record BroadcastDetailDto(
    Guid Id,
    string Title,
    string Subject,
    string Body,
    BroadcastChannel Channels,
    IReadOnlyList<MemberRole> TargetRoles,
    Guid? TargetClubId,
    Guid? TargetEventId,
    bool ActiveMembersOnly,
    BroadcastStatus Status,
    int TotalTargets,
    int DeliveredCount,
    int FailedCount,
    string? FailureReason,
    Guid? CreatedByMemberId,
    DateTime CreatedAtUtc,
    DateTime? ScheduledAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc);

public sealed record DeviceRegistrationDto(
    Guid Id,
    Guid MemberId,
    DevicePlatform Platform,
    string Token,
    bool IsActive,
    DateTime RegisteredAtUtc,
    DateTime LastSeenAtUtc);

public sealed record FeedbackDto(
    Guid Id,
    FeedbackSubjectType SubjectType,
    Guid? SubjectId,
    int Rating,
    string Comment,
    Guid? AuthorMemberId,
    string AuthorName,
    string? AuthorEmail,
    FeedbackStatus Status,
    string? AdminNotes,
    DateTime CreatedAtUtc,
    DateTime? ModeratedAtUtc);

public sealed record SocialHighlightDto(
    Guid Id,
    SocialPlatform Platform,
    string Caption,
    string ExternalUrl,
    string? EmbedHtml,
    string? MediaUrl,
    int DisplayOrder,
    bool IsPublished,
    DateTime? PostedAtUtc,
    DateTime CreatedAtUtc);

public sealed record ChatConversationDto(
    Guid Id,
    Guid? MemberId,
    Guid? GuestKey,
    string Title,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<ChatMessageDto> Messages);

public sealed record ChatMessageDto(
    Guid Id,
    ChatMessageRole Role,
    string Content,
    DateTime CreatedAtUtc);
