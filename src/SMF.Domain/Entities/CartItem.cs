namespace SMF.Domain.Entities;

/// <summary>
/// A single line in a <see cref="Cart"/>. Created via <see cref="Cart.AddItem"/>;
/// price + name are snapshotted at the moment the item enters the cart so the
/// user-visible total is stable until checkout.
/// </summary>
public class CartItem
{
    public Guid Id { get; private set; }
    public Guid CartId { get; private set; }

    public Guid ProductId { get; private set; }
    public string ProductSku { get; private set; } = default!;
    public string ProductName { get; private set; } = default!;
    public string? ImageUrl { get; private set; }

    public long UnitPriceMinor { get; private set; }
    public int Quantity { get; private set; }

    public long LineTotalMinor => checked(UnitPriceMinor * Quantity);

    private CartItem() { }

    internal static CartItem Create(Guid cartId, Product product, int quantity)
    {
        if (cartId == Guid.Empty) throw new ArgumentException("Cart id is required.", nameof(cartId));
        if (product is null) throw new ArgumentNullException(nameof(product));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));

        return new CartItem
        {
            Id = Guid.NewGuid(),
            CartId = cartId,
            ProductId = product.Id,
            ProductSku = product.Sku,
            ProductName = product.Name,
            ImageUrl = product.ImageUrl,
            UnitPriceMinor = product.PriceMinor,
            Quantity = quantity
        };
    }

    internal void IncreaseQuantity(int by)
    {
        if (by <= 0) throw new ArgumentOutOfRangeException(nameof(by));
        Quantity = checked(Quantity + by);
    }

    internal void SetQuantity(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        Quantity = quantity;
    }
}
