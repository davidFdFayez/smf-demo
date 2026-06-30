using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Invoicing;

/// <summary>
/// Server-side PDF renderer backed by QuestPDF. Activated when
/// <c>Invoicing:EnableQuestPdfRenderer=true</c>.
///
/// Licensing: QuestPDF Community is free for individuals and organizations
/// with annual revenue under $1M USD. Above that threshold the Professional
/// licence is required (see https://www.questpdf.com/license/). The license
/// type is set at startup via <see cref="QuestPDF.Settings.License"/>; we
/// default to Community here. Override before <c>app.Run()</c> if you've
/// purchased a Professional licence.
/// </summary>
internal sealed class QuestPdfInvoiceRenderer : IInvoiceRenderer
{
    public InvoiceFormat Format => InvoiceFormat.Pdf;

    public Task<InvoiceDocument> RenderAsync(
        Invoice invoice, CancellationToken cancellationToken = default)
    {
        var bytes = Document.Create(builder =>
        {
            builder.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(t => t.FontSize(10).FontFamily(Fonts.Calibri));

                page.Header().Element(e => Header(e, invoice));
                page.Content().PaddingVertical(8).Element(e => Body(e, invoice));
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Thank you for your support of the Saudi MuayThai Federation.")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        }).GeneratePdf();

        return Task.FromResult(new InvoiceDocument(
            ContentType: "application/pdf",
            Content: bytes,
            FileName: $"invoice-{invoice.InvoiceNumber}.pdf"));
    }

    private static void Header(IContainer container, Invoice inv) =>
        container.PaddingBottom(10).BorderBottom(2).BorderColor("#047857").Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("SMF").FontSize(22).Bold().FontColor("#047857");
                col.Item().Text(inv.IssuerName).FontSize(10).FontColor(Colors.Grey.Darken2);
                if (!string.IsNullOrWhiteSpace(inv.IssuerTaxNumber))
                    col.Item().Text($"VAT {inv.IssuerTaxNumber}").FontSize(8).FontColor(Colors.Grey.Darken1);
                if (!string.IsNullOrWhiteSpace(inv.IssuerAddress))
                    col.Item().Text(inv.IssuerAddress).FontSize(8).FontColor(Colors.Grey.Darken1);
            });

            row.ConstantItem(180).AlignRight().Column(col =>
            {
                col.Item().AlignRight().Background("#dcfce7").Padding(4)
                    .Text("PAID").FontSize(8).Bold().FontColor("#166534").LetterSpacing(0.05f);
                col.Item().PaddingTop(4).Text("Invoice").FontSize(14).Bold();
                col.Item().Text(inv.InvoiceNumber).FontSize(11).Bold().FontColor("#0f172a");
                col.Item().Text($"Issued {inv.IssuedAtUtc:yyyy-MM-dd}").FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        });

    private static void Body(IContainer container, Invoice inv) =>
        container.Column(col =>
        {
            col.Item().PaddingBottom(14).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("BILL TO").FontSize(8).FontColor(Colors.Grey.Darken1).LetterSpacing(0.05f);
                    c.Item().Text(inv.BuyerName).Bold();
                    c.Item().Text(inv.BuyerEmail).FontSize(9).FontColor(Colors.Grey.Darken2);
                    if (!string.IsNullOrWhiteSpace(inv.BuyerTaxNumber))
                        c.Item().Text($"VAT {inv.BuyerTaxNumber}").FontSize(9).FontColor(Colors.Grey.Darken2);
                });
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("REFERENCE").FontSize(8).FontColor(Colors.Grey.Darken1).LetterSpacing(0.05f);
                    c.Item().Text($"Payment: {inv.PaymentId}").FontSize(9);
                    if (inv.OrderId is { } orderId)
                        c.Item().Text($"Order: {orderId}").FontSize(9);
                });
            });

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(5);
                    c.ConstantColumn(40);
                    c.ConstantColumn(80);
                    c.ConstantColumn(80);
                });

                table.Header(h =>
                {
                    h.Cell().Background("#f0fdf4").Padding(6).Text("Description").Bold().FontSize(9).FontColor("#065f46");
                    h.Cell().Background("#f0fdf4").Padding(6).AlignRight().Text("Qty").Bold().FontSize(9).FontColor("#065f46");
                    h.Cell().Background("#f0fdf4").Padding(6).AlignRight().Text("Unit").Bold().FontSize(9).FontColor("#065f46");
                    h.Cell().Background("#f0fdf4").Padding(6).AlignRight().Text("Total").Bold().FontSize(9).FontColor("#065f46");
                });

                foreach (var line in inv.Items)
                {
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(line.Description);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignRight().Text(line.Quantity.ToString());
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignRight().Text(FormatMoney(line.UnitPriceMinor, inv.Currency));
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignRight().Text(FormatMoney(line.LineTotalMinor, inv.Currency));
                }
            });

            col.Item().PaddingTop(12).AlignRight().Width(220).Column(totals =>
            {
                totals.Item().Row(r => { r.RelativeItem().Text("Subtotal").FontColor(Colors.Grey.Darken2); r.ConstantItem(110).AlignRight().Text(FormatMoney(inv.SubtotalMinor, inv.Currency)); });
                totals.Item().Row(r => { r.RelativeItem().Text($"VAT ({inv.VatRateBp / 100m:0.##}%)").FontColor(Colors.Grey.Darken2); r.ConstantItem(110).AlignRight().Text(FormatMoney(inv.VatAmountMinor, inv.Currency)); });
                totals.Item().PaddingTop(6).BorderTop(2).BorderColor("#047857").PaddingTop(6).Row(r =>
                {
                    r.RelativeItem().Text("Amount due").Bold().FontColor("#047857");
                    r.ConstantItem(110).AlignRight().Text(FormatMoney(inv.TotalMinor, inv.Currency)).Bold().FontColor("#047857").FontSize(13);
                });
            });
        });

    private static string FormatMoney(long minor, string currency) =>
        $"{minor / 100m:N2} {currency}";
}
