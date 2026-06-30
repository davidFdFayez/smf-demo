using FluentValidation;
using MediatR;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Members.Commands.RecordMedicalClearance;

public sealed record RecordMedicalClearanceCommand(
    Guid MemberId,
    bool Cleared,
    decimal? WeightCategoryKg) : IRequest<Unit>;

public sealed class RecordMedicalClearanceCommandValidator
    : AbstractValidator<RecordMedicalClearanceCommand>
{
    public RecordMedicalClearanceCommandValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.WeightCategoryKg)
            .GreaterThan(0).LessThanOrEqualTo(250)
            .When(x => x.WeightCategoryKg.HasValue);
    }
}

public sealed class RecordMedicalClearanceCommandHandler
    : IRequestHandler<RecordMedicalClearanceCommand, Unit>
{
    private readonly IMemberRepository _members;

    public RecordMedicalClearanceCommandHandler(IMemberRepository members)
    {
        _members = members;
    }

    public async Task<Unit> Handle(
        RecordMedicalClearanceCommand request,
        CancellationToken cancellationToken)
    {
        var member = await _members.GetByIdAsync(request.MemberId, cancellationToken)
            ?? throw new NotFoundException("Member", request.MemberId);

        // The medical clearance workflow is only meaningful for competing
        // athletes — reject it for any other role so the endpoint can't be
        // used to mark, say, a coach "medically cleared".
        if (member.Role != MemberRole.Athlete)
            throw new InvalidOperationException(
                $"Medical clearance only applies to athletes; member is {member.Role}.");

        member.RecordMedicalClearance(request.Cleared);
        if (request.WeightCategoryKg is { } kg)
            member.SetWeightCategory(kg);

        await _members.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
