using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Enums;

namespace SMF.Infrastructure.Payments;

/// <summary>
/// Sandbox implementation of <see cref="IPaymentGatewayService"/>.
///
/// Every production payment gateway has its own wire protocol, so this class
/// intentionally fakes the happy-path behaviour in-process: we mint a
/// transaction id on <see cref="InitializePayment"/>, cache the expected
/// terminal state, and replay it on <see cref="VerifyPayment"/>.
///
/// The swap-in point for the real MADA / Apple Pay acquirer is a single-class
/// change: implement the same interface with real HTTP calls and rebind it in
/// <see cref="SMF.Infrastructure.DependencyInjection"/>.
/// </summary>
public sealed class MadaPaymentGatewayService : IPaymentGatewayService
{
    private readonly IOptionsMonitor<PaymentGatewayOptions> _options;
    private readonly ILogger<MadaPaymentGatewayService> _logger;

    // Test harness (and the webhook happy path) calls SetVerificationResult
    // with the value VerifyPayment should return for a given txn id. A real
    // gateway implementation would hit its provider API instead of this dict.
    private readonly ConcurrentDictionary<string, PaymentVerificationResult> _verifications = new();

    public MadaPaymentGatewayService(
        IOptionsMonitor<PaymentGatewayOptions> options,
        ILogger<MadaPaymentGatewayService> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task<PaymentInitializationResult> InitializePayment(
        PaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var txnId = $"SBX-{request.Provider}-{Guid.NewGuid():N}".ToUpperInvariant();

        // Pre-seed the expected post-payment state so tests + dev can invoke
        // the webhook without a real provider callback. Real gateways update
        // this asynchronously as the user completes the 3-DS challenge.
        _verifications[txnId] = new PaymentVerificationResult(
            txnId,
            PaymentStatus.Succeeded,
            request.AmountMinor,
            request.Currency);

        var baseUrl = _options.CurrentValue.SandboxCheckoutBaseUrl.TrimEnd('/');
        var redirect = $"{baseUrl}?txn={Uri.EscapeDataString(txnId)}" +
                       $"&callback={Uri.EscapeDataString(request.CallbackUrl)}";

        _logger.LogInformation(
            "[sandbox] Initialized {Provider} {Purpose} — txn={Txn}, amount={Amount} {Currency}",
            request.Provider, request.Purpose, txnId, request.AmountMinor, request.Currency);

        return Task.FromResult(new PaymentInitializationResult(txnId, redirect));
    }

    public Task<PaymentVerificationResult> VerifyPayment(
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        if (_verifications.TryGetValue(transactionId, out var result))
            return Task.FromResult(result);

        _logger.LogWarning("[sandbox] VerifyPayment called for unknown txn {Txn}.", transactionId);
        return Task.FromResult(new PaymentVerificationResult(
            transactionId,
            PaymentStatus.Failed,
            0,
            "SAR",
            FailureReason: "Unknown transaction (sandbox)"));
    }

    /// <summary>
    /// Test hook — lets integration tests override the VerifyPayment result
    /// before driving the webhook (to simulate declines, timeouts, etc.).
    /// No-op for production callers.
    /// </summary>
    public void SetVerificationResult(PaymentVerificationResult result)
        => _verifications[result.TransactionId] = result;
}
