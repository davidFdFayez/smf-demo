using MediatR;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;

namespace SMF.Application.Features.Matches.Commands.AssignTimekeeper;

public sealed class AssignTimekeeperCommandHandler
    : IRequestHandler<AssignTimekeeperCommand, Unit>
{
    private readonly IMatchRepository _matches;
    private readonly IMemberRepository _members;

    public AssignTimekeeperCommandHandler(IMatchRepository matches, IMemberRepository members)
    {
        _matches = matches;
        _members = members;
    }

    public async Task<Unit> Handle(AssignTimekeeperCommand request, CancellationToken cancellationToken)
    {
        var match = await _matches.GetByCodeAsync(request.MatchCode, cancellationToken)
                    ?? throw new NotFoundException("Match", request.MatchCode);

        var member = await _members.GetByIdAsync(request.TimekeeperId, cancellationToken)
                     ?? throw new NotFoundException("Member", request.TimekeeperId);

        // Timekeepers don't need a special role — in small federations the job
        // rotates among referees / officials — so we only verify the member
        // exists and is otherwise valid.
        match.AssignTimekeeper(request.TimekeeperId);
        await _matches.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
