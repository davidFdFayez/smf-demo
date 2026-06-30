namespace SMF.Domain.Entities;

/// <summary>
/// A purchasable item in the federation e-commerce store.
///
/// Stock is tracked in <see cref="StockOnHand"/>: <see cref="ReserveStock"/>
/// decrements at order creation, <see cref="ReleaseStock"/> restores it on
/// cancellation, and <see cref="RestockBy"/> covers operator restocks.
///
/// Concurrency: writes are guarded by the <see cref="RowVersion"/> token so
/// two simultaneous orders cannot oversell the last unit. Conflicts surface
/// as <see cref="DbUpdateConcurrencyException"/> at <c>SaveChangesAsync</c>;
/// the checkout handler retries.
/// </summary>
public class Product
{
    public Guid Id { get; private set; }
    public Guid CategoryId { get; private set; }
    public string Sku { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public string? ImageUrl { get; private set; }

    /// <summary>Price in minor units (halalas for SAR), excluding VAT.</summary>
    public long PriceMinor { get; private set; }
    public string Currency { get; private set; } = "SAR";

    public int StockOnHand { get; private set; }

    /// <summary>Reorder threshold — purely informational, surfaces as a low-stock badge.</summary>
    public int? LowStockThreshold { get; private set; }

    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>
    /// EF rowversion. Prevents two concurrent orders from independently
    /// reading the same stock value and both decrementing it.
    /// </summary>
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private Product() { }

    public static Product Create(
        Guid categoryId,
        string sku,
        string name,
        string? description,
        string? imageUrl,
        long priceMinor,
        string currency,
        int initialStock,
        int? lowStockThreshold,
        DateTime nowUtc)
    {
        if (categoryId == Guid.Empty)
            throw new ArgumentException("Category id is required.", nameof(categoryId));
        if (string.IsNullOrWhiteSpace(sku))
            throw new ArgumentException("SKU is required.", nameof(sku));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name is required.", nameof(name));
        if (priceMinor <= 0)
            throw new ArgumentOutOfRangeException(nameof(priceMinor), "Price must be positive.");
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("Currency must be an ISO-4217 code.", nameof(currency));
        if (initialStock < 0)
            throw new ArgumentOutOfRangeException(nameof(initialStock));
        if (lowStockThreshold is < 0)
            throw new ArgumentOutOfRangeException(nameof(lowStockThreshold));

        return new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = categoryId,
            Sku = sku.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim(),
            PriceMinor = priceMinor,
            Currency = currency.ToUpperInvariant(),
            StockOnHand = initialStock,
            LowStockThreshold = lowStockThreshold,
            IsActive = true,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    public void UpdateDetails(
        string name,
        string? description,
        string? imageUrl,
        long priceMinor,
        Guid categoryId,
        int? lowStockThreshold,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name is required.", nameof(name));
        if (priceMinor <= 0)
            throw new ArgumentOutOfRangeException(nameof(priceMinor), "Price must be positive.");
        if (categoryId == Guid.Empty)
            throw new ArgumentException("Category id is required.", nameof(categoryId));
        if (lowStockThreshold is < 0)
            throw new ArgumentOutOfRangeException(nameof(lowStockThreshold));

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
        PriceMinor = priceMinor;
        CategoryId = categoryId;
        LowStockThreshold = lowStockThreshold;
        UpdatedAtUtc = nowUtc;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    /// <summary>Operator-driven absolute stock adjustment (delta, can be negative).</summary>
    public void RestockBy(int delta, DateTime nowUtc)
    {
        var next = checked(StockOnHand + delta);
        if (next < 0)
            throw new InvalidOperationException(
                $"Stock adjustment of {delta} would make stock negative ({StockOnHand} on hand).");
        StockOnHand = next;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Reserve <paramref name="quantity"/> units for an outgoing order.
    /// Throws when stock is insufficient — caller maps to a 409 / domain error.
    /// </summary>
    public void ReserveStock(int quantity, DateTime nowUtc)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        if (!IsActive)
            throw new InvalidOperationException($"Product '{Sku}' is not available for purchase.");
        if (StockOnHand < quantity)
            throw new InvalidOperationException(
                $"Insufficient stock for '{Sku}'. Requested {quantity}, available {StockOnHand}.");

        StockOnHand -= quantity;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Return reserved stock back to <see cref="StockOnHand"/> on cancellation.</summary>
    public void ReleaseStock(int quantity, DateTime nowUtc)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        StockOnHand = checked(StockOnHand + quantity);
        UpdatedAtUtc = nowUtc;
    }
}
