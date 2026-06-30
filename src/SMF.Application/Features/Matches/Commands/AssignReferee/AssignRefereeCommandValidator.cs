using FluentValidation;

namespace SMF.Application.Features.Matches.Commands.AssignReferee;

public sealed class AssignRefereeCommandValidator : AbstractValidator<AssignRefereeCommand>
{
    public AssignRefereeCommandValidator()
    {
        RuleFor(x => x.MatchCode).NotEmpty();
        RuleFor(x => x.RefereeId).NotEqual(Guid.Empty);
    }
}
