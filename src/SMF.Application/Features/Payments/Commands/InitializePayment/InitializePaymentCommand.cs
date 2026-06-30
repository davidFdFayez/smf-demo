using MediatR;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Payments.Commands.InitializePayment;

/// <summary>
/// Starts a payment attempt: persists a <see cref="SMF.Domain.Entities.Payment"/>
/// row in Pending state and returns the gateway redirect URL the frontend
/// should hand off to.
/// </summary>
public sealed record InitializePaymentCommand(
    Guid MemberId,
    PaymentProvider Provider,
    PaymentPurpose Purpose,
    long AmountMinor,
    string Currency,
    string CallbackUrl,
    string? Description = null,
    Guid? EventRegistrationId = null) : IRequest<InitializePaymentResult>;

public sealed record InitializePaymentResult(
    Guid PaymentId,
    string ProviderTransactionId,
    string RedirectUrl);
