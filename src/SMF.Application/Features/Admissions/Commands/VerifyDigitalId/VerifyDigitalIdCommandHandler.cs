using MediatR;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Admissions.Commands.VerifyDigitalId;

public sealed class VerifyDigitalIdCommandHandler
    : IRequestHandler<VerifyDigitalIdCommand, VerifyDigitalIdResult>
{
    private readonly IDigitalIdTokenService _tokenService;
    private readonly IMemberRepository _members;

    public VerifyDigitalIdCommandHandler(
        IDigitalIdTokenService tokenService,
        IMemberRepository members)
    {
        _tokenService = tokenService;
        _members = members;
    }

    public async Task<VerifyDigitalIdResult> Handle(
        VerifyDigitalIdCommand request,
        CancellationToken cancellationToken)
    {
        var verification = _tokenService.Verify(request.Token ?? string.Empty);

        if (!verification.IsValid)
        {
            // Pass the verifier's iat/exp back out for rejection codes
            // that actually have them (expired / not-yet-valid). That
            // lets the gate UI say "expired at 14:22" instead of a
            // generic "denied".
            return verification.Reason switch
            {
                DigitalIdRejectionReason.Malformed => Deny(
                    AdmissionOutcome.TokenMalformed,
                    "Token is malformed."),

                DigitalIdRejectionReason.SignatureMismatch => Deny(
                    AdmissionOutcome.SignatureMismatch,
                    "Signature check failed — token is forged or from a different environment."),

                DigitalIdRejectionReason.NotYetValid => Deny(
                    AdmissionOutcome.NotYetValid,
                    "Token is not yet valid. Check the device clock.",
                    issuedAt: verification.IssuedAtUtc,
                    validUntil: verification.ValidUntilUtc),

                DigitalIdRejectionReason.Expired => Deny(
                    AdmissionOutcome.Expired,
                    "Token has expired. Athlete must refresh online.",
                    issuedAt: verification.IssuedAtUtc,
                    validUntil: verification.ValidUntilUtc),

                _ => Deny(AdmissionOutcome.TokenMalformed, "Token rejected."),
            };
        }

        var claims = verification.Claims!;

        // Re-check the member's live status — the whole point of this
        // re-check is to catch post-issue revocations within the
        // token's validity window.
        var member = await _members.GetByIdAsync(claims.MemberId, cancellationToken);
        if (member is null)
        {
            return Deny(
                AdmissionOutcome.MemberUnknown,
                "Token references a member that no longer exists.",
                issuedAt: verification.IssuedAtUtc,
                validUntil: verification.ValidUntilUtc,
                claims: claims);
        }

        var currentStatus = member.RegistrationStatus;
        if (!IsAdmissible(currentStatus))
        {
            return new VerifyDigitalIdResult(
                Outcome: AdmissionOutcome.MemberNotInGoodStanding,
                Reason: $"Membership status is '{currentStatus}'. Admission denied.",
                MemberId: member.Id,
                SmfId: member.SMF_ID,
                FullName: member.FullName,
                CurrentStatus: currentStatus,
                StatusAtIssue: claims.StatusAtIssue,
                IssuedAtUtc: verification.IssuedAtUtc,
                ValidUntilUtc: verification.ValidUntilUtc);
        }

        return new VerifyDigitalIdResult(
            Outcome: AdmissionOutcome.Admitted,
            Reason: "Admitted.",
            MemberId: member.Id,
            SmfId: member.SMF_ID,
            FullName: member.FullName,
            CurrentStatus: currentStatus,
            StatusAtIssue: claims.StatusAtIssue,
            IssuedAtUtc: verification.IssuedAtUtc,
            ValidUntilUtc: verification.ValidUntilUtc);
    }

    // Admission policy: approved (registration + payment complete) or
    // active (post first-scan activation). Pending members cannot enter.
    private static bool IsAdmissible(RegistrationStatus status) =>
        status is RegistrationStatus.Approved or RegistrationStatus.Active;

    private static VerifyDigitalIdResult Deny(
        AdmissionOutcome outcome,
        string reason,
        DateTime? issuedAt = null,
        DateTime? validUntil = null,
        DigitalIdClaims? claims = null)
        => new(
            Outcome: outcome,
            Reason: reason,
            MemberId: claims?.MemberId,
            SmfId: claims?.SmfId,
            FullName: claims?.FullName,
            CurrentStatus: null,
            StatusAtIssue: claims?.StatusAtIssue,
            IssuedAtUtc: issuedAt,
            ValidUntilUtc: validUntil);
}
