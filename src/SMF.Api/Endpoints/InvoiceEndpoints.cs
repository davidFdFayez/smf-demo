using MediatR;
using SMF.Application.Common.Interfaces;
using SMF.Application.Features.Invoicing.Queries;

namespace SMF.Api.Endpoints;

/// <summary>
/// Read-only invoice endpoints. JSON detail at <c>/api/invoices/{id}</c>,
/// rendered document (HTML or PDF) at <c>/api/invoices/{id}/document</c>.
/// A buyer-friendly variant keyed by payment id keeps post-checkout pages
/// from having to know the invoice id ahead of time.
/// </summary>
public static class InvoiceEndpoints
{
    public static IEndpointRouteBuilder MapInvoiceEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/invoices").WithTags("Invoicing");

        grp.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GetInvoiceQuery(id), ct)))
           .WithName("GetInvoice")
           .Produces<InvoiceDto>(StatusCodes.Status200OK)
           .ProducesProblem(StatusCodes.Status404NotFound);

        grp.MapGet("/by-payment/{paymentId:guid}", async (
                Guid paymentId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GetInvoiceForPaymentQuery(paymentId), ct)))
           .WithName("GetInvoiceByPayment")
           .Produces<InvoiceDto>(StatusCodes.Status200OK)
           .ProducesProblem(StatusCodes.Status404NotFound);

        grp.MapGet("/{id:guid}/document", async (
                Guid id, ISender sender, CancellationToken ct, string? format = null) =>
            {
                var requested = ParseFormat(format);
                var doc = await sender.Send(new GetInvoiceDocumentQuery(id, requested), ct);
                return Results.File(doc.Content, doc.ContentType, doc.FileName);
            })
           .WithName("DownloadInvoiceDocument")
           .Produces(StatusCodes.Status200OK)
           .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static InvoiceFormat ParseFormat(string? value) =>
        string.Equals(value, "pdf", StringComparison.OrdinalIgnoreCase)
            ? InvoiceFormat.Pdf
            : InvoiceFormat.Html;
}
