namespace SMF.Domain.Entities;

/// <summary>
/// A single line on an <see cref="Order"/>. All buyer-facing fields are
/// snapshots taken from the cart at checkout; subsequent changes to the
/// product master record do not retroactively change the order total.
/// </summary>
public class OrderItem
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductSku { get; private set; } = default!;
    public string ProductName { get; private set; } = default!;
    public long UnitPriceMinor { get; private set; }
    public int Quantity { get; private set; }
    public long LineTotalMinor => checked(UnitPriceMinor * Quantity);

    private OrderItem() { }

    internal static OrderItem FromCart(Guid orderId, CartItem source)
        => new()
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ProductId = source.ProductId,
            ProductSku = source.ProductSku,
            ProductName = source.ProductName,
            UnitPriceMinor = source.UnitPriceMinor,
            Quantity = source.Quantity
        };
}
