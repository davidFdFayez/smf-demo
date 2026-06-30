using System.Net;
using System.Text;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Invoicing;

/// <summary>
/// Always-available invoice renderer. Emits a self-contained HTML document
/// (UTF-8, inline CSS) the buyer can save or print to PDF directly from the
/// browser. Same convention as <c>HtmlCertificateRenderer</c>, so we never
/// have to ship a PDF dependency to enable invoice downloads.
/// </summary>
internal sealed class HtmlInvoiceRenderer : IInvoiceRenderer
{
    public InvoiceFormat Format => InvoiceFormat.Html;

    public Task<InvoiceDocument> RenderAsync(
        Invoice invoice, CancellationToken cancellationToken = default)
    {
        var html = Build(invoice);
        var bytes = Encoding.UTF8.GetBytes(html);
        var fileName = $"invoice-{invoice.InvoiceNumber}.html";
        return Task.FromResult(new InvoiceDocument("text/html; charset=utf-8", bytes, fileName));
    }

    private static string FormatMoney(long minor, string currency)
    {
        var major = minor / 100m;
        return $"{major:N2} {currency}";
    }

    private static string Build(Invoice inv)
    {
        static string Enc(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

        var rows = new StringBuilder();
        foreach (var line in inv.Items)
        {
            rows.Append("<tr>")
                .Append($"<td>{Enc(line.Description)}</td>")
                .Append($"<td class=\"num\">{line.Quantity}</td>")
                .Append($"<td class=\"num\">{FormatMoney(line.UnitPriceMinor, inv.Currency)}</td>")
                .Append($"<td class=\"num\">{FormatMoney(line.LineTotalMinor, inv.Currency)}</td>")
                .Append("</tr>");
        }

        var vatPct = (inv.VatRateBp / 100m).ToString("0.##");
        var issuerLine = string.Join(" · ", new[]
        {
            inv.IssuerName,
            string.IsNullOrWhiteSpace(inv.IssuerTaxNumber) ? null : $"VAT {inv.IssuerTaxNumber}",
            inv.IssuerAddress
        }.Where(p => !string.IsNullOrWhiteSpace(p))!);

        return $$"""
                 <!doctype html>
                 <html lang="en">
                 <head>
                   <meta charset="utf-8">
                   <title>Invoice {{Enc(inv.InvoiceNumber)}}</title>
                   <style>
                     @page { size: A4; margin: 14mm; }
                     body { margin: 0; padding: 0; font-family: -apple-system, "Segoe UI", Arial, sans-serif;
                            color: #1f2937; font-size: 11pt; }
                     .doc { max-width: 210mm; padding: 14mm; box-sizing: border-box; }
                     header { display: flex; justify-content: space-between; align-items: flex-start;
                              border-bottom: 3px solid #047857; padding-bottom: 16px; margin-bottom: 24px; }
                     .brand { font-size: 22pt; font-weight: 700; color: #047857; letter-spacing: 0.04em; }
                     .meta  { text-align: right; font-size: 10pt; color: #475569; }
                     h1 { font-size: 16pt; margin: 24px 0 8px; color: #0f172a; }
                     .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin-bottom: 24px; }
                     .grid h2 { font-size: 9pt; text-transform: uppercase; color: #64748b;
                                margin: 0 0 4px; letter-spacing: 0.08em; }
                     .grid p  { margin: 0; line-height: 1.55; }
                     table { width: 100%; border-collapse: collapse; margin-top: 8px; }
                     th, td { border-bottom: 1px solid #e2e8f0; padding: 10px 8px; text-align: left;
                              vertical-align: top; }
                     th { background: #f0fdf4; font-weight: 600; font-size: 9.5pt; color: #065f46;
                          text-transform: uppercase; letter-spacing: 0.05em; }
                     .num { text-align: right; font-variant-numeric: tabular-nums; }
                     tfoot td { border-bottom: none; font-size: 10.5pt; }
                     tfoot .total td { border-top: 2px solid #047857; font-weight: 700; font-size: 13pt;
                                       color: #047857; padding-top: 14px; }
                     .badge { display: inline-block; padding: 4px 10px; background: #dcfce7;
                              color: #166534; border-radius: 999px; font-size: 9pt; font-weight: 600;
                              letter-spacing: 0.05em; }
                     footer { margin-top: 48px; padding-top: 16px; border-top: 1px solid #e2e8f0;
                              font-size: 9pt; color: #64748b; line-height: 1.5; }
                   </style>
                 </head>
                 <body>
                   <div class="doc">
                     <header>
                       <div>
                         <div class="brand">SMF</div>
                         <div style="font-size:9.5pt;color:#475569;margin-top:4px;">{{Enc(issuerLine)}}</div>
                       </div>
                       <div class="meta">
                         <div class="badge">PAID</div>
                         <h1 style="margin-top:8px;">Invoice</h1>
                         <div><strong>{{Enc(inv.InvoiceNumber)}}</strong></div>
                         <div>Issued {{inv.IssuedAtUtc:yyyy-MM-dd}}</div>
                       </div>
                     </header>

                     <div class="grid">
                       <div>
                         <h2>Bill to</h2>
                         <p>
                           <strong>{{Enc(inv.BuyerName)}}</strong><br>
                           {{Enc(inv.BuyerEmail)}}{{(string.IsNullOrWhiteSpace(inv.BuyerTaxNumber)
                             ? "" : "<br>VAT " + Enc(inv.BuyerTaxNumber))}}
                         </p>
                       </div>
                       <div>
                         <h2>Reference</h2>
                         <p>
                           Payment: <code>{{Enc(inv.PaymentId.ToString())}}</code>
                           {{(inv.OrderId is null ? "" : "<br>Order: <code>" + Enc(inv.OrderId.Value.ToString()) + "</code>")}}
                         </p>
                       </div>
                     </div>

                     <table>
                       <thead>
                         <tr>
                           <th>Description</th>
                           <th class="num">Qty</th>
                           <th class="num">Unit</th>
                           <th class="num">Total</th>
                         </tr>
                       </thead>
                       <tbody>
                         {{rows}}
                       </tbody>
                       <tfoot>
                         <tr><td colspan="3" class="num">Subtotal</td>
                             <td class="num">{{FormatMoney(inv.SubtotalMinor, inv.Currency)}}</td></tr>
                         <tr><td colspan="3" class="num">VAT ({{vatPct}}%)</td>
                             <td class="num">{{FormatMoney(inv.VatAmountMinor, inv.Currency)}}</td></tr>
                         <tr class="total"><td colspan="3" class="num">Amount due</td>
                             <td class="num">{{FormatMoney(inv.TotalMinor, inv.Currency)}}</td></tr>
                       </tfoot>
                     </table>

                     <footer>
                       Thank you for your support of the Saudi MuayThai Federation.<br>
                       This is an electronically issued invoice. Save or print this page to keep a copy.
                     </footer>
                   </div>
                 </body>
                 </html>
                 """;
    }
}
