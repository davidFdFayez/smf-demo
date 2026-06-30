namespace SMF.Domain.Entities;

/// <summary>
/// Issued automatically when a <see cref="Payment"/> reaches
/// <c>Succeeded</c>. Covers all three purposes — membership fees, event
/// fees, and product orders — by snapshotting buyer + line items at issue
/// time. The invoice is a static, audit-grade record: it never reflects
/// later edits to the source data.
///
/// Numbering format: <c>INV-{yyyy}-{sequence:00000}</c>. The sequence is
/// allocated atomically alongside the invoice insert (similar to the SMF
/// member id), keyed by year so legal year-end accounting stays clean.
/// </summary>
public class Invoice
{
    public Guid Id { get; private set; }
    public string InvoiceNumber { get; private set; } = default!;

    /// <summary>The payment this invoice was issued against.</summary>
    public Guid PaymentId { get; private set; }

    /// <summary>Optional — set for membership/event invoices and product invoices owned by a member.</summary>
    public Guid? MemberId { get; private set; }

    /// <summary>Optional — set for product orders.</summary>
    public Guid? OrderId { get; private set; }

    public string BuyerName { get; private set; } = default!;
    public string BuyerEmail { get; private set; } = default!;

    /// <summary>Optional — VAT registration number for B2B invoices.</summary>
    public string? BuyerTaxNumber { get; private set; }

    /// <summary>Issuer block — federation legal name, VAT number, address line.</summary>
    public string IssuerName { get; private set; } = default!;
    public string? IssuerTaxNumber { get; private set; }
    public string? IssuerAddress { get; private set; }

    public DateTime IssuedAtUtc { get; private set; }

    public long SubtotalMinor { get; private set; }
    public int VatRateBp { get; private set; }
    public long VatAmountMinor { get; private set; }
    public long TotalMinor { get; private set; }
    public string Currency { get; private set; } = "SAR";

    private readonly List<InvoiceLineItem> _items = new();
    public IReadOnlyCollection<InvoiceLineItem> Items => _items.AsReadOnly();

    private Invoice() { }

    public static Invoice Create(
        string invoiceNumber,
        Guid paymentId,
        Guid? memberId,
        Guid? orderId,
        string buyerName,
        string buyerEmail,
        string? buyerTaxNumber,
        string issuerName,
        string? issuerTaxNumber,
        string? issuerAddress,
        IReadOnlyCollection<InvoiceLineItem> items,
        int vatRateBp,
        string currency,
        DateTime issuedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new ArgumentException("Invoice number is required.", nameof(invoiceNumber));
        if (paymentId == Guid.Empty)
            throw new ArgumentException("Payment id is required.", nameof(paymentId));
        if (string.IsNullOrWhiteSpace(buyerName))
            throw new ArgumentException("Buyer name is required.", nameof(buyerName));
        if (string.IsNullOrWhiteSpace(buyerEmail))
            throw new ArgumentException("Buyer email is required.", nameof(buyerEmail));
        if (string.IsNullOrWhiteSpace(issuerName))
            throw new ArgumentException("Issuer name is required.", nameof(issuerName));
        if (items is null || items.Count == 0)
            throw new InvalidOperationException("Invoice must have at least one line item.");
        if (vatRateBp is < 0 or > 10_000)
            throw new ArgumentOutOfRangeException(nameof(vatRateBp));
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("Currency must be ISO-4217.", nameof(currency));

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = invoiceNumber.Trim().ToUpperInvariant(),
            PaymentId = paymentId,
            MemberId = memberId,
            OrderId = orderId,
            BuyerName = buyerName.Trim(),
            BuyerEmail = buyerEmail.Trim().ToLowerInvariant(),
            BuyerTaxNumber = string.IsNullOrWhiteSpace(buyerTaxNumber) ? null : buyerTaxNumber.Trim(),
            IssuerName = issuerName.Trim(),
            IssuerTaxNumber = string.IsNullOrWhiteSpace(issuerTaxNumber) ? null : issuerTaxNumber.Trim(),
            IssuerAddress = string.IsNullOrWhiteSpace(issuerAddress) ? null : issuerAddress.Trim(),
            VatRateBp = vatRateBp,
            Currency = currency.ToUpperInvariant(),
            IssuedAtUtc = issuedAtUtc
        };

        foreach (var item in items)
            invoice._items.Add(item);

        invoice.SubtotalMinor = invoice._items.Sum(i => i.LineTotalMinor);
        invoice.VatAmountMinor = invoice.SubtotalMinor * vatRateBp / 10_000L;
        invoice.TotalMinor = checked(invoice.SubtotalMinor + invoice.VatAmountMinor);
        return invoice;
    }
}
