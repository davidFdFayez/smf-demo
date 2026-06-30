using MediatR;
using Microsoft.Extensions.Logging;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;
using SMF.Application.Features.Payments.Events;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Payments.Commands.ConfirmPaymentCallback;

/// <summary>
/// Processes a single provider-callback delivery:
///
///   1. Loads the local <see cref="SMF.Domain.Entities.Payment"/> by the
///      provider's transaction id (never trusts the webhook body).
///   2. Asks the gateway for the authoritative state via VerifyPayment.
///   3. Drives the aggregate state transition. MarkSucceeded /
///      MarkFailed each report whether the call was a no-op — if the
///      payment was already in that terminal state, we skip publishing
///      the domain event so duplicate webhooks never double-fire side
///      effects.
///   4. Publishes <see cref="PaymentSuccessfulEvent"/> on first success.
/// </summary>
public sealed class ConfirmPaymentCallbackCommandHandler
    : IRequestHandler<ConfirmPaymentCallbackCommand, ConfirmPaymentCallbackResult>
{
    private readonly IPaymentRepository _payments;
    private readonly IPaymentGatewayService _gateway;
    private readonly IOutbox _outbox;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<ConfirmPaymentCallbackCommandHandler> _logger;

    public ConfirmPaymentCallbackCommandHandler(
        IPaymentRepository payments,
        IPaymentGatewayService gateway,
        IOutbox outbox,
        IDateTimeProvider clock,
        ILogger<ConfirmPaymentCallbackCommandHandler> logger)
    {
        _payments = payments;
        _gateway = gateway;
        _outbox = outbox;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ConfirmPaymentCallbackResult> Handle(
        ConfirmPaymentCallbackCommand request,
        CancellationToken cancellationToken)
    {
        var payment = await _payments.GetByProviderTransactionIdAsync(
                          request.ProviderTransactionId, cancellationToken)
                     ?? throw new NotFoundException("Payment", request.ProviderTransactionId);

        // Authoritative check: ask the gateway, don't believe the callback body.
        var verified = await _gateway.VerifyPayment(request.ProviderTransactionId, cancellationToken);

        bool transitioned;
        switch (verified.Status)
        {
            case PaymentStatus.Succeeded:
                transitioned = payment.MarkSucceeded(_clock.UtcNow);
                break;
            case PaymentStatus.Failed:
                transitioned = payment.MarkFailed(verified.FailureReason, _clock.UtcNow);
                break;
            case PaymentStatus.Pending:
                // Provider says it's still pending — likely a premature
                // webhook. Leave the aggregate alone and return 200 so the
                // provider stops retrying this particular delivery.
                _logger.LogInformation(
                    "Callback for {Txn} arrived while provider still reports Pending; ignoring.",
                    request.ProviderTransactionId);
                return new ConfirmPaymentCallbackResult(payment.Id, payment.Status, TransitionedNow: false);
            case PaymentStatus.Refunded:
                _logger.LogWarning(
                    "Callback reports Refunded for {Txn} but refund flow is not wired — skipping.",
                    request.ProviderTransactionId);
                return new ConfirmPaymentCallbackResult(payment.Id, payment.Status, TransitionedNow: false);
            default:
                throw new InvalidOperationException(
                    $"Unexpected provider status '{verified.Status}' for transaction '{request.ProviderTransactionId}'.");
        }

        // Durability: stage the domain event on the outbox BEFORE committing
        // so the Payment state change and the event emission land in the same
        // SQL transaction. If the process crashes right after SaveChangesAsync,
        // the OutboxHostedService picks the row up and publishes it.
        if (transitioned && payment.Status == PaymentStatus.Succeeded)
        {
            await _outbox.EnqueueAsync(
                new PaymentSuccessfulEvent(
                    payment.Id,
                    payment.MemberId,
                    payment.Provider,
                    payment.Purpose,
                    payment.AmountMinor,
                    payment.Currency,
                    payment.ProviderTransactionId,
                    _clock.UtcNow,
                    payment.EventRegistrationId),
                cancellationToken);
        }

        await _payments.SaveChangesAsync(cancellationToken);

        return new ConfirmPaymentCallbackResult(payment.Id, payment.Status, transitioned);
    }
}
