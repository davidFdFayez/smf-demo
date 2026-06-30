using MediatR;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Admissions.Commands.VerifyDigitalId;

/// <summary>
/// Scans a Digital ID token at the venue gate. The gate app sends the
/// raw QR payload; we verify signature + window, then re-check the
/// member's current status so a post-issue suspension still denies
/// admission within the token's validity window.
/// </summary>
public sealed record VerifyDigitalIdCommand(string Token)
    : IRequest<VerifyDigitalIdResult>;

public enum AdmissionOutcome
{
    Admitted = 0,
    TokenMalformed = 1,
    SignatureMismatch = 2,
    NotYetValid = 3,
    Expired = 4,
    MemberUnknown = 5,
    MemberNotInGoodStanding = 6,
}

public sealed record VerifyDigitalIdResult(
    AdmissionOutcome Outcome,
    string Reason,
    Guid? MemberId,
    string? SmfId,
    string? FullName,
    RegistrationStatus? CurrentStatus,
    RegistrationStatus? StatusAtIssue,
    DateTime? IssuedAtUtc,
    DateTime? ValidUntilUtc)
{
    public bool Admitted => Outcome == AdmissionOutcome.Admitted;
}
