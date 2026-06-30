using FluentValidation;

namespace SMF.Application.Features.Members.Queries.GetMembers;

public sealed class GetMembersQueryValidator : AbstractValidator<GetMembersQuery>
{
    public GetMembersQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}
