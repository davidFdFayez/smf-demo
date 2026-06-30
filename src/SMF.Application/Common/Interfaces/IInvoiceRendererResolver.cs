namespace SMF.Application.Common.Interfaces;

/// <summary>
/// Resolves a concrete <see cref="IInvoiceRenderer"/> for a requested
/// <see cref="InvoiceFormat"/>. Falls back to HTML when the requested
/// format is not registered (e.g. PDF requested but QuestPDF disabled).
/// </summary>
public interface IInvoiceRendererResolver
{
    IInvoiceRenderer Resolve(InvoiceFormat format);
    bool IsSupported(InvoiceFormat format);
}
