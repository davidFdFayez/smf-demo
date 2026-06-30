using MediatR;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Entities;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Matches.Commands.ScheduleMatch;

public sealed class ScheduleMatchCommandHandler
    : IRequestHandler<ScheduleMatchCommand, ScheduleMatchResult>
{
    private readonly IMatchRepository _matches;
    private readonly IMemberRepository _members;
    private readonly IDateTimeProvider _clock;

    public ScheduleMatchCommandHandler(
        IMatchRepository matches,
        IMemberRepository members,
        IDateTimeProvider clock)
    {
        _matches = matches;
        _members = members;
        _clock = clock;
    }

    public async Task<ScheduleMatchResult> Handle(ScheduleMatchCommand request, CancellationToken cancellationToken)
    {
        var existing = await _matches.GetByCodeAsync(request.Code, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException($"A match with code '{request.Code}' already exists.");

        // Depth-of-defence: before we schedule, confirm every referee id
        // refers to a Member whose Role is Referee. FK protects against
        // non-existent ids; this pass rejects athletes/coaches.
        await EnsureRefereeAsync(request.HeadRefereeId, cancellationToken);
        if (request.SideRefereeIds is not null)
        {
            foreach (var id in request.SideRefereeIds)
                await EnsureRefereeAsync(id, cancellationToken);
        }

        var match = Match.Schedule(
            request.Code,
            request.HeadRefereeId,
            request.ScheduledAtUtc,
            _clock.UtcNow);

        if (request.SideRefereeIds is not null)
        {
            foreach (var id in request.SideRefereeIds)
                match.AssignReferee(id);
        }

        await _matches.AddAsync(match, cancellationToken);
        await _matches.SaveChangesAsync(cancellationToken);

        return new ScheduleMatchResult(match.Id, match.Code);
    }

    private async Task EnsureRefereeAsync(Guid memberId, CancellationToken cancellationToken)
    {
        var member = await _members.GetByIdAsync(memberId, cancellationToken)
                     ?? throw new NotFoundException("Member", memberId);

        if (member.Role != MemberRole.Referee)
            throw new InvalidOperationException(
                $"Member '{memberId}' has role {member.Role}; only Referees can be assigned to a match.");
    }
}
