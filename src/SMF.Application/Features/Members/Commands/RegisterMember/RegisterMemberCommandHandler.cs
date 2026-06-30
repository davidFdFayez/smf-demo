using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMF.Application.Common.Interfaces;
using SMF.Application.Features.Compliance.ParentalConsent;
using SMF.Domain.Entities;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Members.Commands.RegisterMember;

public sealed class RegisterMemberCommandHandler
    : IRequestHandler<RegisterMemberCommand, RegisterMemberResult>
{
    private readonly IMemberRepository _memberRepository;
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly IConsentSigner _signer;
    private readonly IMediator _mediator;
    private readonly ILogger<RegisterMemberCommandHandler> _logger;

    public RegisterMemberCommandHandler(
        IMemberRepository memberRepository,
        IApplicationDbContext db,
        IDateTimeProvider dateTime,
        IConsentSigner signer,
        IMediator mediator,
        ILogger<RegisterMemberCommandHandler> logger)
    {
        _memberRepository = memberRepository;
        _db = db;
        _dateTime = dateTime;
        _signer = signer;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<RegisterMemberResult> Handle(
        RegisterMemberCommand request,
        CancellationToken cancellationToken)
    {
        var year = _dateTime.Today.Year;
        var sequence = await _memberRepository.GetNextSequenceForYearAsync(year, cancellationToken);
        var smfId = BuildSmfId(year, sequence);
        var acceptedAtUtc = DateTime.UtcNow;
        var isMinor = IsMinor(request.DateOfBirth, _dateTime.Today);

        // Minors registered through the new self-service flow start with
        // GuardianConsent=false; the parental ceremony will flip it on
        // approval. Adults (and legacy in-person minor signups) keep the
        // existing semantics.
        var initialGuardianConsent = isMinor && !request.GuardianConsent ? false : request.GuardianConsent;

        var member = Member.Register(
            fullName: request.FullName,
            dateOfBirth: request.DateOfBirth,
            role: request.Role,
            smfId: smfId,
            guardianConsent: initialGuardianConsent,
            email: request.Email,
            phoneNumber: request.PhoneNumber,
            nationalId: request.NationalId,
            acceptedAtUtc: acceptedAtUtc,
            affiliatedClubId: request.AffiliatedClubId,
            licenseLevel: request.LicenseLevel,
            yearsOfExperience: request.YearsOfExperience);

        await _memberRepository.AddAsync(member, cancellationToken);
        await _memberRepository.SaveChangesAsync(cancellationToken);

        // Pin the active policy versions so the audit log can answer
        // "exactly which version of the privacy policy did this user accept".
        await RecordPolicyAcceptancesAsync(member, request, acceptedAtUtc, cancellationToken);

        // Trigger parental-consent workflow for self-service minor signups.
        Guid? consentId = null;
        DateTime? consentExpires = null;
        string? signUrl = null;
        if (isMinor && !request.GuardianConsent && !string.IsNullOrWhiteSpace(request.GuardianFullName))
        {
            var issue = await _mediator.Send(
                new ParentalConsentFeatures.IssueParentalConsentCommand(
                    member.Id,
                    request.GuardianFullName!,
                    request.GuardianRelation ?? GuardianRelation.LegalGuardian,
                    request.GuardianEmail!,
                    request.GuardianPhone!,
                    request.GuardianNationalId,
                    request.IpAddress,
                    request.UserAgent),
                cancellationToken);
            consentId = issue.ConsentId;
            consentExpires = issue.TokenExpiresAtUtc;
            signUrl = issue.GuardianSignUrl;
        }

        _logger.LogInformation(
            "Registered member {MemberId} with SMF_ID {SmfId} as {Role} (compliance accepted at {AcceptedAt:O}; minor={Minor}; consentId={ConsentId})",
            member.Id, member.SMF_ID, member.Role, acceptedAtUtc, isMinor, consentId);

        return new RegisterMemberResult(
            member.Id, member.SMF_ID, member.RegistrationStatus,
            consentId, consentExpires, signUrl);
    }

    private async Task RecordPolicyAcceptancesAsync(
        Member member, RegisterMemberCommand request, DateTime acceptedAtUtc, CancellationToken ct)
    {
        var active = await _db.PolicyDocuments
            .Where(p => p.IsActive && p.EffectiveAtUtc <= acceptedAtUtc)
            .ToListAsync(ct);

        var byKind = active.GroupBy(p => p.Kind)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.EffectiveAtUtc).First());

        void AcceptIf(PolicyDocumentKind kind, bool accepted)
        {
            if (!accepted) return;
            if (!byKind.TryGetValue(kind, out var policy)) return;

            var payload = string.Join('|',
                member.Id, kind, policy.Version, policy.ContentHash, acceptedAtUtc.ToString("O"));
            var hmac = _signer.Sign(payload);

            _db.PolicyAcceptances.Add(PolicyAcceptance.Record(
                member.Id, policy, request.IpAddress, request.UserAgent, hmac));

            _db.ConsentLogs.Add(ConsentLog.Record(
                ConsentEventType.PolicyAccepted,
                member.Id, policy.Id, parentalConsentId: null,
                kind.ToString(), policy.Version, policy.ContentHash,
                request.IpAddress, request.UserAgent,
                detailsJson: null, signatureHmac: hmac));
        }

        AcceptIf(PolicyDocumentKind.TermsOfService,  request.AcceptTerms);
        AcceptIf(PolicyDocumentKind.PrivacyPolicy,   request.AcceptPrivacyPolicy);
        AcceptIf(PolicyDocumentKind.CodeOfConduct,   request.AcceptCodeOfConduct);

        if (byKind.Count > 0) await _db.SaveChangesAsync(ct);
    }

    private static bool IsMinor(DateOnly dob, DateOnly today)
    {
        if (dob == default) return false;
        var age = today.Year - dob.Year;
        if (dob > today.AddYears(-age)) age--;
        return age < 18;
    }

    private static string BuildSmfId(int year, int sequence)
        => $"#SMF{year:D4}-{sequence:D5}";
}
