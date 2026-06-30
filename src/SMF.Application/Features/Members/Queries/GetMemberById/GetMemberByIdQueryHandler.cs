using MediatR;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;

namespace SMF.Application.Features.Members.Queries.GetMemberById;

public sealed class GetMemberByIdQueryHandler
    : IRequestHandler<GetMemberByIdQuery, MemberDetails>
{
    private readonly IMemberRepository _members;

    public GetMemberByIdQueryHandler(IMemberRepository members)
    {
        _members = members;
    }

    public async Task<MemberDetails> Handle(
        GetMemberByIdQuery request,
        CancellationToken cancellationToken)
    {
        var member = await _members.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Member", request.Id);

        return new MemberDetails(
            member.Id,
            member.FullName,
            member.DateOfBirth,
            member.Role,
            member.SMF_ID,
            member.RegistrationStatus,
            member.GuardianConsent,
            member.Email,
            member.PhoneNumber,
            member.NationalId,
            member.CreatedAtUtc,
            member.AffiliatedClubId,
            member.LicenseLevel,
            member.YearsOfExperience,
            member.WeightCategoryKg,
            member.MedicalCleared,
            member.MedicalClearedAtUtc);
    }
}
