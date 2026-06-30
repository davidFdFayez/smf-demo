using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;

namespace SMF.Application.Features.Store.Products.Commands;

public sealed record UpdateProductCommand(
    Guid Id,
    Guid CategoryId,
    string Name,
    string? Description,
    string? ImageUrl,
    long PriceMinor,
    int? LowStockThreshold,
    bool IsActive) : IRequest;

public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.Id).NotEqual(Guid.Empty);
        RuleFor(x => x.CategoryId).NotEqual(Guid.Empty);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.ImageUrl).MaximumLength(1024);
        RuleFor(x => x.PriceMinor).GreaterThan(0);
        RuleFor(x => x.LowStockThreshold).GreaterThanOrEqualTo(0).When(x => x.LowStockThreshold.HasValue);
    }
}

public sealed class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public UpdateProductCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
                      ?? throw new NotFoundException("Product", request.Id);

        var categoryExists = await _db.ProductCategories
            .AnyAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (!categoryExists)
            throw new NotFoundException("ProductCategory", request.CategoryId);

        product.UpdateDetails(
            request.Name, request.Description, request.ImageUrl,
            request.PriceMinor, request.CategoryId,
            request.LowStockThreshold, _clock.UtcNow);

        if (request.IsActive) product.Activate(); else product.Deactivate();

        await _db.SaveChangesAsync(cancellationToken);
    }
}
