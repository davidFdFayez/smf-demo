using FluentValidation;

namespace SMF.Application.Features.Matches.Commands.ScheduleMatch;

public sealed class ScheduleMatchCommandValidator : AbstractValidator<ScheduleMatchCommand>
{
    public ScheduleMatchCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Match code is required.")
            .MaximumLength(64);

        RuleFor(x => x.HeadRefereeId)
            .NotEqual(Guid.Empty).WithMessage("Head referee id is required.");

        RuleForEach(x => x.SideRefereeIds)
            .NotEqual(Guid.Empty).WithMessage("Referee id must be a valid GUID.");
    }
}
