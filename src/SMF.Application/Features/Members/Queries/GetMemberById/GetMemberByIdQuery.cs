using MediatR;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Members.Queries.GetMemberById;

/// <summary>
/// Fetches a single member by id. Used by the athlete mobile client to
/// refresh the cached profile after login and any time connectivity is
/// restored. Returns 404 via <c>NotFoundException</c> when the id is unknown.
/// </summary>
public sealed record GetMemberByIdQuery(Guid Id) : IRequest<MemberDetails>;

public sealed record MemberDetails(
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
