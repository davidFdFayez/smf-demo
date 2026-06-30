using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;
using SMF.Application.Features.Store.Cart.Common;
using SMF.Application.Features.Store.Cart.Queries;
using SMF.Application.Features.Store.Common;

namespace SMF.Application.Features.Store.Cart.Commands;

public sealed record AddCartItemCommand(
    CartIdentity Identity,
    Guid ProductId,
    int Quantity) : IRequest<CartDto>;

public sealed class AddCartItemCommandValidator : AbstractValidator<AddCartItemCommand>
{
    public AddCartItemCommandValidator()
    {
        RuleFor(x => x.Identity).Must(i => i.IsValid)
            .WithMessage("Cart identity must include either a guest key or a member id.");
        RuleFor(x => x.ProductId).NotEqual(Guid.Empty);
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(50);
    }
}

public sealed class AddCartItemCommandHandler : IRequestHandler<AddCartItemCommand, CartDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly IInvoicingSettings _settings;

    public AddCartItemCommandHandler(
        IApplicationDbContext db, IDateTimeProvider clock, IInvoicingSettings settings)
    {
        _db = db;
        _clock = clock;
        _settings = settings;
    }

    public async Task<CartDto> Handle(AddCartItemCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products.FirstOrDefaultAsync(
                          p => p.Id == request.ProductId, cancellationToken)
                      ?? throw new NotFoundException("Product", request.ProductId);

        if (!product.IsActive)
            throw new InvalidOperationException($"Product '{product.Sku}' is not available.");
        if (product.StockOnHand < request.Quantity)
            throw new InvalidOperationException(
                $"Insufficient stock for '{product.Sku}'. Requested {request.Quantity}, available {product.StockOnHand}.");

        var cart = await CartLookup.LoadAsync(_db, request.Identity, cancellationToken);
        if (cart is null)
        {
            cart = request.Identity.MemberId is { } m && m != Guid.Empty
                ? Domain.Entities.Cart.ForMember(m, _settings.DefaultCurrency, _clock.UtcNow)
                : Domain.Entities.Cart.ForGuest(request.Identity.GuestKey!.Value, _settings.DefaultCurrency, _clock.UtcNow);
            _db.Carts.Add(cart);
        }

        cart.AddItem(product, request.Quantity, _clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);

        return await new GetCartQueryHandler(_db, _settings)
            .Handle(new GetCartQuery(request.Identity), cancellationToken);
    }
}
