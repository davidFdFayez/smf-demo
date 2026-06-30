using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;

namespace SMF.Application.Features.Invoicing.Queries;

/// <summary>
/// Renders the invoice in the requested <see cref="InvoiceFormat"/>. The
/// renderer resolver falls back to HTML when PDF is asked for but not
/// configured (no QuestPDF on the build).
/// </summary>
public sealed record GetInvoiceDocumentQuery(Guid InvoiceId, InvoiceFormat Format)
    : IRequest<InvoiceDocument>;

public sealed class GetInvoiceDocumentQueryHandler
    : IRequestHandler<GetInvoiceDocumentQuery, InvoiceDocument>
{
    private readonly IApplicationDbContext _db;
    private readonly IInvoiceRendererResolver _resolver;

    public GetInvoiceDocumentQueryHandler(
        IApplicationDbContext db, IInvoiceRendererResolver resolver)
    {
        _db = db;
        _resolver = resolver;
    }

    public async Task<InvoiceDocument> Handle(
        GetInvoiceDocumentQuery request, CancellationToken cancellationToken)
    {
        var invoice = await _db.Invoices
                          .Include(i => i.Items)
                          .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken)
                      ?? throw new NotFoundException("Invoice", request.InvoiceId);

        var renderer = _resolver.Resolve(request.Format);
        return await renderer.RenderAsync(invoice, cancellationToken);
    }
}
