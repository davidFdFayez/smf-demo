using MediatR;
using Microsoft.Extensions.Logging;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Payments.Events;

/// <summary>
/// Listens to <see cref="PaymentSuccessfulEvent"/> and, when the payment was
/// for the membership fee, promotes the member to
/// <see cref="SMF.Domain.Enums.RegistrationStatus.Active"/>.
///
/// Other purposes (<see cref="PaymentPurpose.EventFee"/>, ...) are ignored
/// here — they live under their own handlers alongside this one, all
/// listening to the same event.
/// </summary>
public sealed class ActivateMemberOnPaymentSucceededHandler
    : INotificationHandler<PaymentSuccessfulEvent>
{
    private readonly IMemberRepository _members;
    private readonly ILogger<ActivateMemberOnPaymentSucceededHandler> _logger;

    public ActivateMemberOnPaymentSucceededHandler(
        IMemberRepository members,
        ILogger<ActivateMemberOnPaymentSucceededHandler> logger)
    {
        _members = members;
        _logger = logger;
    }

    public async Task Handle(PaymentSuccessfulEvent notification, CancellationToken cancellationToken)
    {
        if (notification.Purpose != PaymentPurpose.MembershipFee)
            return;
        if (notification.MemberId is not { } memberId)
            return;

        var member = await _members.GetByIdAsync(memberId, cancellationToken)
                     ?? throw new NotFoundException("Member", memberId);

        // ActivateAfterPayment is itself idempotent — a no-op if the member
        // is already Active — so double-firing is safe.
        member.ActivateAfterPayment();
        await _members.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Activated member {MemberId} after membership-fee payment {PaymentId} ({Provider}:{Txn}).",
            member.Id,
            notification.PaymentId,
            notification.Provider,
            notification.ProviderTransactionId);
    }
}
