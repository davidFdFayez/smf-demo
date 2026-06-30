namespace SMF.Domain.Entities;

/// <summary>
/// One row on an <see cref="Invoice"/>. Frozen at issuance time — pure
/// snapshot, no FK back to the source product/event/membership.
/// </summary>
public class InvoiceLineItem
{
    public Guid Id { get; private set; }
    public Guid InvoiceId { get; private set; }

    /// <summary>Free-text description shown on the printed invoice.</summary>
    public string Description { get; private set; } = default!;
    public int Quantity { get; private set; }
    public long UnitPriceMinor { get; private set; }
    public long LineTotalMinor => checked(UnitPriceMinor * Quantity);

    private InvoiceLineItem() { }

    public static InvoiceLineItem Create(string description, int quantity, long unitPriceMinor)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Line description is required.", nameof(description));
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        if (unitPriceMinor < 0)
            throw new ArgumentOutOfRangeException(nameof(unitPriceMinor));

        return new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            Description = description.Trim(),
            Quantity = quantity,
            UnitPriceMinor = unitPriceMinor
        };
    }
}
