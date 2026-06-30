using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Entities;

namespace SMF.Application.Features.Store.Products.Commands;

public sealed record CreateProductCommand(
    Guid CategoryId,
    string Sku,
    string Name,
    string? Description,
    string? ImageUrl,
    long PriceMinor,
    string Currency,
    int InitialStock,
    int? LowStockThreshold) : IRequest<Guid>;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.CategoryId).NotEqual(Guid.Empty);
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(64).Matches("^[A-Z0-9-_]+$")
            .WithMessage("SKU must be uppercase alphanumerics, dashes or underscores.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.ImageUrl).MaximumLength(1024);
        RuleFor(x => x.PriceMinor).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.InitialStock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.LowStockThreshold).GreaterThanOrEqualTo(0).When(x => x.LowStockThreshold.HasValue);
    }
}

public sealed class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public CreateProductCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Guid> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var categoryExists = await _db.ProductCategories
            .AnyAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (!categoryExists)
            throw new NotFoundException("ProductCategory", request.CategoryId);

        var sku = request.Sku.Trim().ToUpperInvariant();
        var skuTaken = await _db.Products.AnyAsync(p => p.Sku == sku, cancellationToken);
        if (skuTaken)
            throw new InvalidOperationException($"SKU '{sku}' is already in use.");

        var product = Product.Create(
            request.CategoryId, sku, request.Name, request.Description, request.ImageUrl,
            request.PriceMinor, request.Currency, request.InitialStock,
            request.LowStockThreshold, _clock.UtcNow);

        _db.Products.Add(product);
        await _db.SaveChangesAsync(cancellationToken);
        return product.Id;
    }
}
