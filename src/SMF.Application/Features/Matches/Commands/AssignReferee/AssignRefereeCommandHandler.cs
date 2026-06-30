using MediatR;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Matches.Commands.AssignReferee;

public sealed class AssignRefereeCommandHandler
    : IRequestHandler<AssignRefereeCommand, Unit>
{
    private readonly IMatchRepository _matches;
    private readonly IMemberRepository _members;

    public AssignRefereeCommandHandler(IMatchRepository matches, IMemberRepository members)
    {
        _matches = matches;
        _members = members;
    }

    public async Task<Unit> Handle(AssignRefereeCommand request, CancellationToken cancellationToken)
    {
        var match = await _matches.GetByCodeAsync(request.MatchCode, cancellationToken)
                    ?? throw new NotFoundException("Match", request.MatchCode);

        var member = await _members.GetByIdAsync(request.RefereeId, cancellationToken)
                     ?? throw new NotFoundException("Member", request.RefereeId);

        if (member.Role != MemberRole.Referee)
            throw new InvalidOperationException(
                $"Member '{request.RefereeId}' has role {member.Role}; only Referees can be assigned.");

        match.AssignReferee(request.RefereeId);
        await _matches.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
