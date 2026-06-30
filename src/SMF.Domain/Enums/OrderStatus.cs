namespace SMF.Domain.Enums;

public enum OrderStatus
{
    /// <summary>Created from a cart, stock reserved, waiting for the gateway to capture funds.</summary>
    AwaitingPayment = 0,

    /// <summary>Payment captured. Stock reservation is now consumed; ready for fulfilment.</summary>
    Paid = 1,

    /// <summary>Operator marked the order as shipped. Customer-visible.</summary>
    Fulfilled = 2,

    /// <summary>Cancelled before fulfilment (manual, payment failure, or timeout). Stock returned.</summary>
    Cancelled = 3,

    /// <summary>Funds were returned after capture. Stock not automatically returned.</summary>
    Refunded = 4
}
