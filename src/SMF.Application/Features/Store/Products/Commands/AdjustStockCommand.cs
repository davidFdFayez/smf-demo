using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;

namespace SMF.Application.Features.Store.Products.Commands;

/// <summary>Operator-driven stock adjustment. Positive delta = restock, negative = write-off.</summary>
public sealed record AdjustStockCommand(Guid ProductId, int Delta, string? Reason = null) : IRequest;

public sealed class AdjustStockCommandValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEqual(Guid.Empty);
        RuleFor(x => x.Delta).NotEqual(0);
        RuleFor(x => x.Reason).MaximumLength(256);
    }
}

public sealed class AdjustStockCommandHandler : IRequestHandler<AdjustStockCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public AdjustStockCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task Handle(AdjustStockCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException("Product", request.ProductId);

        product.RestockBy(request.Delta, _clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
