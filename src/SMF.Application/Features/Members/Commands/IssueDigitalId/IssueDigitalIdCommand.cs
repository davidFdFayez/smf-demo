using MediatR;
using SMF.Application.Features.Members.Queries.GetMemberById;

namespace SMF.Application.Features.Members.Commands.IssueDigitalId;

/// <summary>
/// Issue a signed Digital ID token for the given member. Called by the
/// athlete mobile client at login and on refresh — the returned token
/// goes into the on-device Hive cache so the QR renders offline at the
/// venue gate.
/// </summary>
public sealed record IssueDigitalIdCommand(Guid MemberId) : IRequest<IssueDigitalIdResult>;

public sealed record IssueDigitalIdResult(
    MemberDetails Member,
    string Token,
    DateTime IssuedAtUtc,
    DateTime ValidUntilUtc);
