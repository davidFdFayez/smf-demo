using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Interfaces;
using SMF.Application.Features.Store.Cart.Common;
using SMF.Application.Features.Store.Common;

namespace SMF.Application.Features.Store.Cart.Queries;

public sealed record GetCartQuery(CartIdentity Identity) : IRequest<CartDto>;

public sealed class GetCartQueryHandler : IRequestHandler<GetCartQuery, CartDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IInvoicingSettings _settings;

    public GetCartQueryHandler(IApplicationDbContext db, IInvoicingSettings settings)
    {
        _db = db;
        _settings = settings;
    }

    public async Task<CartDto> Handle(GetCartQuery request, CancellationToken cancellationToken)
    {
        var cart = await CartLookup.LoadAsync(_db, request.Identity, cancellationToken);

        if (cart is null)
        {
            return new CartDto(
                Guid.Empty,
                request.Identity.GuestKey,
                request.Identity.MemberId,
                _settings.DefaultCurrency,
                0, _settings.DefaultVatRateBp, 0, 0,
                Array.Empty<CartLineDto>());
        }

        var lines = cart.Items
            .Select(i => new CartLineDto(
                i.ProductId, i.ProductSku, i.ProductName, i.ImageUrl,
                i.UnitPriceMinor, i.Quantity, i.LineTotalMinor))
            .ToList();

        var subtotal = cart.SubtotalMinor;
        var vat = subtotal * _settings.DefaultVatRateBp / 10_000L;
        return new CartDto(
            cart.Id, cart.GuestKey, cart.MemberId, cart.Currency,
            subtotal, _settings.DefaultVatRateBp, vat, subtotal + vat,
            lines);
    }
}

internal static class CartLookup
{
    public static Task<Domain.Entities.Cart?> LoadAsync(
        IApplicationDbContext db, CartIdentity identity, CancellationToken ct)
    {
        if (identity.MemberId is { } m && m != Guid.Empty)
            return db.Carts.Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.MemberId == m, ct);
        if (identity.GuestKey is { } g && g != Guid.Empty)
            return db.Carts.Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.GuestKey == g, ct);
        return Task.FromResult<Domain.Entities.Cart?>(null);
    }
}
