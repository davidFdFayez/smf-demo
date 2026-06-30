using SMF.Domain.Entities;

namespace SMF.Application.Common.Interfaces;

/// <summary>
/// Renders an <see cref="Invoice"/> into a downloadable document. Two
/// implementations ship out of the box:
///
///   * <c>HtmlInvoiceRenderer</c> — always available, returns a
///     standalone HTML file the browser can print to PDF.
///   * <c>QuestPdfInvoiceRenderer</c> — bound when the QuestPDF feature
///     flag is on, returns a real <c>application/pdf</c> document.
///
/// Endpoints choose the format via <see cref="InvoiceFormat"/>; the renderer
/// resolver in DI picks the right concrete implementation.
/// </summary>
public interface IInvoiceRenderer
{
    InvoiceFormat Format { get; }

    Task<InvoiceDocument> RenderAsync(
        Invoice invoice,
        CancellationToken cancellationToken = default);
}

public enum InvoiceFormat
{
    Html = 0,
    Pdf = 1
}

public sealed record InvoiceDocument(
    string ContentType,
    byte[] Content,
    string FileName);
