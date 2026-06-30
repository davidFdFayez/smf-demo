using MediatR;

namespace SMF.Application.Features.Matches.Commands.AssignTimekeeper;

/// <summary>
/// Assigns the timekeeper for a scheduled match. The timekeeper owns the
/// round clock via the SignalR hub, independently of the referee team.
/// </summary>
public sealed record AssignTimekeeperCommand(string MatchCode, Guid TimekeeperId) : IRequest<Unit>;
