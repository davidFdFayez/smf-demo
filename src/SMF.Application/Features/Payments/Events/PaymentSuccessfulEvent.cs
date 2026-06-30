using MediatR;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Payments.Events;

/// <summary>
/// Raised when the gateway confirms a payment has been successfully captured.
///
/// This is an in-process MediatR <see cref="INotification"/> — every registered
/// <c>INotificationHandler&lt;PaymentSuccessfulEvent&gt;</c> gets a chance to
/// react (e.g. activate the member, enrol them in the event, send receipt).
///
/// Publishing happens inside the ConfirmPaymentCallback command handler, only
/// on the first transition to <see cref="PaymentStatus.Succeeded"/>, so
/// duplicate webhook deliveries don't fan out more than once.
/// </summary>
public sealed record PaymentSuccessfulEvent(
    Guid PaymentId,
    Guid? MemberId,
    PaymentProvider Provider,
    PaymentPurpose Purpose,
    long AmountMinor,
    string Currency,
    string ProviderTransactionId,
    DateTime OccurredAtUtc,
    Guid? EventRegistrationId = null) : INotification;
