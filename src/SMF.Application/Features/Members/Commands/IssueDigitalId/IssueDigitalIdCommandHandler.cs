using MediatR;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;
using SMF.Application.Features.Members.Queries.GetMemberById;

namespace SMF.Application.Features.Members.Commands.IssueDigitalId;

public sealed class IssueDigitalIdCommandHandler
    : IRequestHandler<IssueDigitalIdCommand, IssueDigitalIdResult>
{
    private readonly IMemberRepository _members;
    private readonly IDigitalIdTokenService _tokenService;

    public IssueDigitalIdCommandHandler(
        IMemberRepository members,
        IDigitalIdTokenService tokenService)
    {
        _members = members;
        _tokenService = tokenService;
    }

    public async Task<IssueDigitalIdResult> Handle(
        IssueDigitalIdCommand request,
        CancellationToken cancellationToken)
    {
        var member = await _members.GetByIdAsync(request.MemberId, cancellationToken)
            ?? throw new NotFoundException("Member", request.MemberId);

        // The token embeds the status AT ISSUE. The gate still re-checks
        // the live status on Verify so a post-issue revocation is caught
        // even within the token's validity window.
        var token = _tokenService.Issue(new DigitalIdClaims(
            MemberId: member.Id,
            SmfId: member.SMF_ID,
            FullName: member.FullName,
            StatusAtIssue: member.RegistrationStatus));

        var details = new MemberDetails(
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

        return new IssueDigitalIdResult(
            Member: details,
            Token: token.Token,
            IssuedAtUtc: token.IssuedAtUtc,
            ValidUntilUtc: token.ValidUntilUtc);
    }
}
