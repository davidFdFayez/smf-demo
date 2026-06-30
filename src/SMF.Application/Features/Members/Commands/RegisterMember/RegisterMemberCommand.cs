using MediatR;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Members.Commands.RegisterMember;

/// <summary>
/// Registers a new federation member. Aligns with PDF §3 ("Membership &amp;
/// Registration") and §8 ("Privacy &amp; Terms"): the request must carry the
/// contact fields required to reach the member and explicit acceptance of
/// the three compliance policies. Acceptance is recorded as server-side
/// timestamps, not raw booleans, for audit traceability.
///
/// When the applicant's <see cref="DateOfBirth"/> is under 18 the request
/// must instead carry the four <c>Guardian*</c> fields. The handler issues
/// a parental-consent ceremony and returns the secure signing URL on the
/// result so the frontend can display "we just emailed your guardian" UX.
/// </summary>
public sealed record RegisterMemberCommand(
    string FullName,
    DateOnly DateOfBirth,
    MemberRole Role,
    bool GuardianConsent,
    string Email,
    string PhoneNumber,
    string NationalId,
    bool AcceptTerms,
    bool AcceptPrivacyPolicy,
    bool AcceptCodeOfConduct,
    Guid? AffiliatedClubId = null,
    string? LicenseLevel = null,
    int? YearsOfExperience = null,
    // Minor-athlete parental-consent workflow fields (new). Optional at the
    // command level; the validator enforces them when DOB &lt; 18.
    string? GuardianFullName = null,
    GuardianRelation? GuardianRelation = null,
    string? GuardianEmail = null,
    string? GuardianPhone = null,
    string? GuardianNationalId = null,
    string? IpAddress = null,
    string? UserAgent = null
) : IRequest<RegisterMemberResult>;

public sealed record RegisterMemberResult(
    Guid Id,
    string SMF_ID,
    RegistrationStatus RegistrationStatus,
    Guid? ParentalConsentId,
    DateTime? ParentalConsentExpiresAtUtc,
    string? GuardianSignUrl
);
