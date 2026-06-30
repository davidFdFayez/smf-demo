using MediatR;

namespace SMF.Application.Features.Matches.Commands.ScheduleMatch;

public sealed record ScheduleMatchCommand(
    string Code,
    Guid HeadRefereeId,
    DateTime ScheduledAtUtc,
    IReadOnlyCollection<Guid>? SideRefereeIds = null) : IRequest<ScheduleMatchResult>;

public sealed record ScheduleMatchResult(Guid Id, string Code);
