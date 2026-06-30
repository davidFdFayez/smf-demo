using MediatR;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Members.Queries.GetMembers;

public sealed record GetMembersQuery(
    int Page = 1,
    int PageSize = 25,
    string? Search = null,
    MemberRole? Role = null,
    RegistrationStatus? Status = null)
    : IRequest<PagedResult<MemberListItem>>;

public sealed record MemberListItem(
    Guid Id,
    string FullName,
    DateOnly DateOfBirth,
    MemberRole Role,
    string SMF_ID,
    RegistrationStatus RegistrationStatus,
    bool GuardianConsent,
    string Email,
    string PhoneNumber,
    string NationalId,
    DateTime CreatedAtUtc,
    Guid? AffiliatedClubId = null,
    string? LicenseLevel = null,
    int? YearsOfExperience = null,
    decimal? WeightCategoryKg = null,
    bool? MedicalCleared = null,
    DateTime? MedicalClearedAtUtc = null);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize);
