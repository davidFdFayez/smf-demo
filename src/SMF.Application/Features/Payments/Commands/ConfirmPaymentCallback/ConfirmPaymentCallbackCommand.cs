using MediatR;

namespace SMF.Application.Features.Payments.Commands.ConfirmPaymentCallback;

/// <summary>
/// Dispatched by the webhook controller after the provider posts back. The
/// command is deliberately thin — just a provider transaction id — because
/// the handler never trusts the webhook body; it calls the provider back via
/// <see cref="SMF.Application.Common.Interfaces.IPaymentGatewayService.VerifyPayment"/>
/// to get the authoritative state.
/// </summary>
public sealed record ConfirmPaymentCallbackCommand(string ProviderTransactionId)
    : IRequest<ConfirmPaymentCallbackResult>;

public sealed record ConfirmPaymentCallbackResult(
    Guid PaymentId,
    SMF.Domain.Enums.PaymentStatus Status,
    bool TransitionedNow);
