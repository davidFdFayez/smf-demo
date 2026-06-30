using SMF.Domain.Enums;
using SMF.Domain.ValueObjects;

namespace SMF.Domain.Entities;

/// <summary>
/// E-commerce order. Created from a <see cref="Cart"/> at checkout — at which
/// point all items have their unit price snapshotted onto the line, stock is
/// reserved on each <see cref="Product"/>, and the order enters
/// <see cref="OrderStatus.AwaitingPayment"/>.
///
/// The <see cref="Status"/> state machine:
/// <code>
///   AwaitingPayment ──succeed──▶ Paid ──ship──▶ Fulfilled
///         │                        │
///         ├──fail/timeout──▶ Cancelled (stock returned)
///         └────────────────▶ Refunded (post-capture)
/// </code>
/// </summary>
public class Order
{
    public Guid Id { get; private set; }

    /// <summary>Human-friendly identifier surfaced to the buyer.</summary>
    public string OrderNumber { get; private set; } = default!;

    /// <summary>Optional — set when checkout is linked to an SMF member profile.</summary>
    public Guid? MemberId { get; private set; }

    public string BuyerName { get; private set; } = default!;
    public string BuyerEmail { get; private set; } = default!;

    public ShippingAddress ShippingAddress { get; private set; } = default!;

    public long SubtotalMinor { get; private set; }

    /// <summary>VAT rate in basis points (1500 = 15%, KSA standard).</summary>
    public int VatRateBp { get; private set; }
    public long VatAmountMinor { get; private set; }
    public long ShippingFeeMinor { get; private set; }
    public long TotalMinor { get; private set; }
    public string Currency { get; private set; } = "SAR";

    public OrderStatus Status { get; private set; }

    /// <summary>Set after the gateway transaction is created; used by the webhook handler.</summary>
    public Guid? PaymentId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? PaidAtUtc { get; private set; }
    public DateTime? FulfilledAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }
    public string? CancellationReason { get; private set; }

    private readonly List<OrderItem> _items = new();
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    private Order() { }

    /// <summary>
    /// Build an order from the cart. Snapshots prices + names. Computes
    /// VAT inclusive of <paramref name="vatRateBp"/> on the subtotal. Caller
    /// must call <see cref="Product.ReserveStock"/> on each product in the
    /// same unit of work.
    /// </summary>
    public static Order CreateFromCart(
        string orderNumber,
        Guid? memberId,
        string buyerName,
        string buyerEmail,
        ShippingAddress shippingAddress,
        IReadOnlyCollection<CartItem> cartItems,
        int vatRateBp,
        long shippingFeeMinor,
        string currency,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
            throw new ArgumentException("Order number is required.", nameof(orderNumber));
        if (string.IsNullOrWhiteSpace(buyerName))
            throw new ArgumentException("Buyer name is required.", nameof(buyerName));
        if (string.IsNullOrWhiteSpace(buyerEmail))
            throw new ArgumentException("Buyer email is required.", nameof(buyerEmail));
        if (shippingAddress is null) throw new ArgumentNullException(nameof(shippingAddress));
        if (cartItems is null || cartItems.Count == 0)
            throw new InvalidOperationException("Cannot create an order from an empty cart.");
        if (vatRateBp is < 0 or > 10_000)
            throw new ArgumentOutOfRangeException(nameof(vatRateBp), "VAT rate must be 0–10000 basis points.");
        if (shippingFeeMinor < 0)
            throw new ArgumentOutOfRangeException(nameof(shippingFeeMinor));
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("Currency must be ISO-4217.", nameof(currency));

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = orderNumber.Trim().ToUpperInvariant(),
            MemberId = memberId,
            BuyerName = buyerName.Trim(),
            BuyerEmail = buyerEmail.Trim().ToLowerInvariant(),
            ShippingAddress = shippingAddress,
            VatRateBp = vatRateBp,
            ShippingFeeMinor = shippingFeeMinor,
            Currency = currency.ToUpperInvariant(),
            Status = OrderStatus.AwaitingPayment,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };

        foreach (var ci in cartItems)
        {
            order._items.Add(OrderItem.FromCart(order.Id, ci));
        }

        order.SubtotalMinor = order._items.Sum(i => i.LineTotalMinor);
        // VAT calculation is integer-safe (no float drift): subtotal × bp ÷ 10000.
        order.VatAmountMinor = order.SubtotalMinor * vatRateBp / 10_000L;
        order.TotalMinor = checked(order.SubtotalMinor + order.VatAmountMinor + shippingFeeMinor);

        return order;
    }

    public void AttachPayment(Guid paymentId, DateTime nowUtc)
    {
        if (paymentId == Guid.Empty)
            throw new ArgumentException("Payment id is required.", nameof(paymentId));
        if (Status != OrderStatus.AwaitingPayment)
            throw new InvalidOperationException(
                $"Order {OrderNumber} is {Status} — payment is no longer attachable.");
        PaymentId = paymentId;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Idempotent: returns false on subsequent calls so duplicate webhooks no-op.</summary>
    public bool MarkPaid(DateTime nowUtc)
    {
        if (Status == OrderStatus.Paid || Status == OrderStatus.Fulfilled || Status == OrderStatus.Refunded)
            return false;
        if (Status == OrderStatus.Cancelled)
            throw new InvalidOperationException(
                $"Order {OrderNumber} is cancelled and cannot be marked paid.");
        Status = OrderStatus.Paid;
        PaidAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
        return true;
    }

    public void MarkFulfilled(DateTime nowUtc)
    {
        if (Status != OrderStatus.Paid)
            throw new InvalidOperationException(
                $"Order {OrderNumber} cannot be fulfilled from status {Status}.");
        Status = OrderStatus.Fulfilled;
        FulfilledAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Cancel the order. Caller is expected to release stock on each line item.</summary>
    public bool Cancel(string? reason, DateTime nowUtc)
    {
        if (Status == OrderStatus.Cancelled) return false;
        if (Status == OrderStatus.Refunded || Status == OrderStatus.Fulfilled)
            throw new InvalidOperationException(
                $"Order {OrderNumber} is {Status} and cannot be cancelled.");
        Status = OrderStatus.Cancelled;
        CancellationReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        CancelledAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
        return true;
    }

    public void MarkRefunded(DateTime nowUtc)
    {
        if (Status != OrderStatus.Paid && Status != OrderStatus.Fulfilled)
            throw new InvalidOperationException(
                $"Order {OrderNumber} cannot be refunded from status {Status}.");
        Status = OrderStatus.Refunded;
        UpdatedAtUtc = nowUtc;
    }
}
