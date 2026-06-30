using SMF.Domain.Enums;

namespace SMF.Domain.Entities;

/// <summary>
/// Record of a single payment attempt for a member.
///
/// Lifecycle: <see cref="PaymentStatus.Pending"/> → <see cref="PaymentStatus.Succeeded"/> | <see cref="PaymentStatus.Failed"/>.
/// Transitions are idempotent so repeated webhook deliveries from the provider
/// do not produce duplicate domain events.
/// </summary>
public class Payment
{
    public Guid Id { get; private set; }

    /// <summary>
    /// Optional federation member. <c>null</c> for guest e-commerce checkouts
    /// (<see cref="PaymentPurpose.ProductPurchase"/>); always set for
    /// <see cref="PaymentPurpose.MembershipFee"/> and
    /// <see cref="PaymentPurpose.EventFee"/>.
    /// </summary>
    public Guid? MemberId { get; private set; }

    public PaymentProvider Provider { get; private set; }

    public PaymentPurpose Purpose { get; private set; }

    /// <summary>Gateway-assigned transaction identifier. Unique per provider.</summary>
    public string ProviderTransactionId { get; private set; } = default!;

    /// <summary>Amount in minor units (halalas for SAR). Avoids float drift.</summary>
    public long AmountMinor { get; private set; }

    /// <summary>ISO-4217 currency code, e.g. "SAR".</summary>
    public string Currency { get; private set; } = default!;

    public PaymentStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    /// <summary>
    /// Set when the provider reports a failure — captured verbatim so ops can
    /// correlate with their dashboard, not exposed to end users.
    /// </summary>
    public string? FailureReason { get; private set; }

    /// <summary>
    /// Optional correlation to an <see cref="EventRegistration"/> when the
    /// payment is for an <see cref="PaymentPurpose.EventFee"/>. Lets the
    /// <c>PaymentSuccessfulEvent</c> handler confirm exactly the right
    /// registration without guessing by member + event.
    /// </summary>
    public Guid? EventRegistrationId { get; private set; }

    private Payment() { }

    private Payment(
        Guid id,
        Guid? memberId,
        PaymentProvider provider,
        PaymentPurpose purpose,
        string providerTransactionId,
        long amountMinor,
        string currency,
        DateTime createdAtUtc)
    {
        Id = id;
        MemberId = memberId;
        Provider = provider;
        Purpose = purpose;
        ProviderTransactionId = providerTransactionId;
        AmountMinor = amountMinor;
        Currency = currency;
        Status = PaymentStatus.Pending;
        CreatedAtUtc = createdAtUtc;
    }

    public static Payment Initiate(
        Guid? memberId,
        PaymentProvider provider,
        PaymentPurpose purpose,
        string providerTransactionId,
        long amountMinor,
        string currency,
        DateTime nowUtc)
    {
        // Guest e-commerce checkouts have a null member id; member-bound
        // purposes (MembershipFee, EventFee) must carry one.
        if (purpose != PaymentPurpose.ProductPurchase &&
            (memberId is null || memberId == Guid.Empty))
            throw new ArgumentException(
                $"MemberId is required for purpose '{purpose}'.", nameof(memberId));
        if (memberId == Guid.Empty) memberId = null;

        if (string.IsNullOrWhiteSpace(providerTransactionId))
            throw new ArgumentException("Provider transaction id is required.", nameof(providerTransactionId));
        if (amountMinor <= 0)
            throw new ArgumentOutOfRangeException(nameof(amountMinor), "Amount must be positive.");
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("Currency must be an ISO-4217 code.", nameof(currency));

        return new Payment(
            Guid.NewGuid(),
            memberId,
            provider,
            purpose,
            providerTransactionId,
            amountMinor,
            currency.ToUpperInvariant(),
            nowUtc);
    }

    /// <summary>
    /// Marks this payment as succeeded. Returns <c>true</c> the first time the
    /// transition happens and <c>false</c> on subsequent calls, so callers can
    /// decide whether to publish a domain event / run side effects.
    /// </summary>
    public bool MarkSucceeded(DateTime nowUtc)
    {
        if (Status == PaymentStatus.Succeeded) return false;

        if (Status == PaymentStatus.Refunded)
            throw new InvalidOperationException(
                $"Payment '{Id}' is already refunded and cannot be re-succeeded.");

        Status = PaymentStatus.Succeeded;
        CompletedAtUtc = nowUtc;
        FailureReason = null;
        return true;
    }

    /// <summary>Associate this payment with an event registration (idempotent).</summary>
    public void AttachEventRegistration(Guid eventRegistrationId)
    {
        if (eventRegistrationId == Guid.Empty)
            throw new ArgumentException("Registration id is required.", nameof(eventRegistrationId));
        EventRegistrationId = eventRegistrationId;
    }

    public bool MarkFailed(string? reason, DateTime nowUtc)
    {
        if (Status == PaymentStatus.Failed) return false;
        if (Status == PaymentStatus.Succeeded)
            throw new InvalidOperationException(
                $"Payment '{Id}' already succeeded; cannot mark as failed. Use refund flow instead.");

        Status = PaymentStatus.Failed;
        CompletedAtUtc = nowUtc;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "Unspecified failure" : reason.Trim();
        return true;
    }
}
