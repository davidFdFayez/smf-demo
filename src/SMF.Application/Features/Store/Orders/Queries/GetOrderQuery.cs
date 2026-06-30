using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;
using SMF.Application.Features.Store.Common;

namespace SMF.Application.Features.Store.Orders.Queries;

public sealed record GetOrderQuery(Guid Id) : IRequest<OrderDto>;

public sealed class GetOrderQueryHandler : IRequestHandler<GetOrderQuery, OrderDto>
{
    private readonly IApplicationDbContext _db;

    public GetOrderQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<OrderDto> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Order", request.Id);

        var invoiceId = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.OrderId == request.Id)
            .Select(i => (Guid?)i.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var addr = order.ShippingAddress;
        return new OrderDto(
            order.Id, order.OrderNumber, order.MemberId, order.BuyerName, order.BuyerEmail,
            new OrderShippingAddressDto(
                addr.RecipientName, addr.Line1, addr.Line2, addr.City, addr.Region,
                addr.PostalCode, addr.Country, addr.PhoneNumber),
            order.SubtotalMinor, order.VatRateBp, order.VatAmountMinor,
            order.ShippingFeeMinor, order.TotalMinor, order.Currency,
            order.Status, order.PaymentId, invoiceId,
            order.CreatedAtUtc, order.PaidAtUtc,
            order.Items.Select(i => new OrderLineDto(
                i.ProductId, i.ProductSku, i.ProductName, i.UnitPriceMinor,
                i.Quantity, i.LineTotalMinor)).ToList());
    }
}
