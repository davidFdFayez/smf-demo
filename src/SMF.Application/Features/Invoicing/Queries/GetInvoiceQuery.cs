using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;

namespace SMF.Application.Features.Invoicing.Queries;

public sealed record GetInvoiceQuery(Guid Id) : IRequest<InvoiceDto>;

public sealed record InvoiceLineDto(string Description, int Quantity, long UnitPriceMinor, long LineTotalMinor);

public sealed record InvoiceDto(
    Guid Id,
    string InvoiceNumber,
    Guid PaymentId,
    Guid? OrderId,
    Guid? MemberId,
    string BuyerName,
    string BuyerEmail,
    string? BuyerTaxNumber,
    string IssuerName,
    string? IssuerTaxNumber,
    string? IssuerAddress,
    DateTime IssuedAtUtc,
    long SubtotalMinor,
    int VatRateBp,
    long VatAmountMinor,
    long TotalMinor,
    string Currency,
    IReadOnlyCollection<InvoiceLineDto> Items);

public sealed class GetInvoiceQueryHandler : IRequestHandler<GetInvoiceQuery, InvoiceDto>
{
    private readonly IApplicationDbContext _db;

    public GetInvoiceQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<InvoiceDto> Handle(GetInvoiceQuery request, CancellationToken cancellationToken)
    {
        var inv = await _db.Invoices
                      .AsNoTracking()
                      .Include(i => i.Items)
                      .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
                  ?? throw new NotFoundException("Invoice", request.Id);

        return Map(inv);
    }

    internal static InvoiceDto Map(Domain.Entities.Invoice inv) => new(
        inv.Id, inv.InvoiceNumber, inv.PaymentId, inv.OrderId, inv.MemberId,
        inv.BuyerName, inv.BuyerEmail, inv.BuyerTaxNumber,
        inv.IssuerName, inv.IssuerTaxNumber, inv.IssuerAddress,
        inv.IssuedAtUtc, inv.SubtotalMinor, inv.VatRateBp, inv.VatAmountMinor,
        inv.TotalMinor, inv.Currency,
        inv.Items.Select(i => new InvoiceLineDto(
            i.Description, i.Quantity, i.UnitPriceMinor, i.LineTotalMinor)).ToList());
}

public sealed record GetInvoiceForPaymentQuery(Guid PaymentId) : IRequest<InvoiceDto>;

public sealed class GetInvoiceForPaymentQueryHandler
    : IRequestHandler<GetInvoiceForPaymentQuery, InvoiceDto>
{
    private readonly IApplicationDbContext _db;

    public GetInvoiceForPaymentQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<InvoiceDto> Handle(
        GetInvoiceForPaymentQuery request, CancellationToken cancellationToken)
    {
        var inv = await _db.Invoices
                      .AsNoTracking()
                      .Include(i => i.Items)
                      .FirstOrDefaultAsync(i => i.PaymentId == request.PaymentId, cancellationToken)
                  ?? throw new NotFoundException("Invoice", $"payment={request.PaymentId}");

        return GetInvoiceQueryHandler.Map(inv);
    }
}
