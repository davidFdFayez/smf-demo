namespace SMF.Domain.Enums;

public enum PaymentStatus
{
    /// <summary>Initialized locally, redirect URL issued, waiting for the user to pay.</summary>
    Pending = 0,

    /// <summary>Provider confirmed the funds were captured.</summary>
    Succeeded = 1,

    /// <summary>Provider reported a hard failure (declined, timed-out, cancelled).</summary>
    Failed = 2,

    /// <summary>Funds were returned to the cardholder after capture.</summary>
    Refunded = 3
}
