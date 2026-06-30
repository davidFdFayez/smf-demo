using FluentValidation;
using MediatR;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Entities;

namespace SMF.Application.Features.Store.Categories.Commands;

public sealed record CreateCategoryCommand(
    string Name,
    string Slug,
    string? Description,
    string? ImageUrl,
    int DisplayOrder = 0) : IRequest<Guid>;

public sealed class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Slug)
            .NotEmpty().MaximumLength(120)
            .Matches("^[a-z0-9-]+$")
            .WithMessage("Slug must be lowercase alphanumerics and dashes.");
        RuleFor(x => x.Description).MaximumLength(1024);
        RuleFor(x => x.ImageUrl).MaximumLength(1024);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public CreateCategoryCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Guid> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = ProductCategory.Create(
            request.Name, request.Slug, request.Description,
            request.ImageUrl, request.DisplayOrder, _clock.UtcNow);

        _db.ProductCategories.Add(category);
        await _db.SaveChangesAsync(cancellationToken);
        return category.Id;
    }
}
