using MediatR;

namespace SMF.Application.Features.Matches.Commands.AssignReferee;

public sealed record AssignRefereeCommand(string MatchCode, Guid RefereeId) : IRequest<Unit>;
