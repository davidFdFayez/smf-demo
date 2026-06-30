using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;

namespace SMF.Application.Features.Store.Orders.Commands;

public sealed record FulfillOrderCommand(Guid OrderId) : IRequest;

public sealed class FulfillOrderCommandValidator : AbstractValidator<FulfillOrderCommand>
{
    public FulfillOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEqual(Guid.Empty);
    }
}

public sealed class FulfillOrderCommandHandler : IRequestHandler<FulfillOrderCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<FulfillOrderCommandHandler> _logger;

    public FulfillOrderCommandHandler(
        IApplicationDbContext db, IDateTimeProvider clock,
        ILogger<FulfillOrderCommandHandler> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task Handle(FulfillOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
                    ?? throw new NotFoundException("Order", request.OrderId);

        order.MarkFulfilled(_clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Order {OrderNumber} marked fulfilled.", order.OrderNumber);
    }
}

public sealed record CancelOrderCommand(Guid OrderId, string? Reason = null) : IRequest;

public sealed class CancelOrderCommandValidator : AbstractValidator<CancelOrderCommand>
{
    public CancelOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEqual(Guid.Empty);
        RuleFor(x => x.Reason).MaximumLength(512);
    }
}

public sealed class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<CancelOrderCommandHandler> _logger;

    public CancelOrderCommandHandler(
        IApplicationDbContext db, IDateTimeProvider clock,
        ILogger<CancelOrderCommandHandler> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _db.Orders
                        .Include(o => o.Items)
                        .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
                    ?? throw new NotFoundException("Order", request.OrderId);

        var transitioned = order.Cancel(request.Reason, _clock.UtcNow);
        if (!transitioned) return;

        // Return reserved stock for every line — operator-driven cancels keep
        // the storefront inventory in sync.
        var productIds = order.Items.Select(i => i.ProductId).ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);
        foreach (var line in order.Items)
        {
            if (products.TryGetValue(line.ProductId, out var product))
                product.ReleaseStock(line.Quantity, _clock.UtcNow);
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Order {OrderNumber} cancelled and stock released ({LineCount} lines).",
            order.OrderNumber, order.Items.Count);
    }
}
