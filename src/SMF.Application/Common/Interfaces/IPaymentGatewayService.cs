using SMF.Domain.Enums;

namespace SMF.Application.Common.Interfaces;

/// <summary>
/// Anti-corruption layer between the application and whichever Saudi payment
/// gateway the Infrastructure layer is bound to (MADA, Apple Pay, ...).
///
/// The interface is deliberately minimal — just <see cref="InitializePayment"/>
/// (start a checkout session) and <see cref="VerifyPayment"/> (independent
/// confirmation of the terminal state). Webhook payloads are never trusted on
/// their own; <see cref="VerifyPayment"/> always calls the provider back.
/// </summary>
public interface IPaymentGatewayService
{
    /// <summary>
    /// Opens a new checkout session with the provider and returns the URL the
    /// browser / native SDK should redirect the payer to, plus the transaction
    /// id we'll persist on <see cref="SMF.Domain.Entities.Payment.ProviderTransactionId"/>.
    /// </summary>
    Task<PaymentInitializationResult> InitializePayment(
        PaymentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Independently confirms the state of a transaction against the
    /// provider's API. This is the authoritative call — do not trust the
    /// webhook body alone; anyone can POST to the callback URL.
    /// </summary>
    Task<PaymentVerificationResult> VerifyPayment(
        string transactionId,
        CancellationToken cancellationToken = default);
}

public sealed record PaymentRequest(
    Guid MemberId,
    PaymentProvider Provider,
    PaymentPurpose Purpose,
    long AmountMinor,
    string Currency,
    string CallbackUrl,
    string? Description = null);

public sealed record PaymentInitializationResult(
    string TransactionId,
    string RedirectUrl);

public sealed record PaymentVerificationResult(
    string TransactionId,
    PaymentStatus Status,
    long AmountMinor,
    string Currency,
    string? FailureReason = null);
