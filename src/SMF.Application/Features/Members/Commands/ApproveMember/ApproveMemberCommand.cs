using MediatR;

namespace SMF.Application.Features.Members.Commands.ApproveMember;

public sealed record ApproveMemberCommand(Guid MemberId) : IRequest<Unit>;
