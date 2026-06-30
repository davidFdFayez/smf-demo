using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Payments.Events;

/// <summary>
/// Listens to <see cref="PaymentSuccessfulEvent"/> and, when the payment was
/// tagged as an <see cref="PaymentPurpose.EventFee"/> with a correlated
/// <c>EventRegistrationId</c>, promotes the registration to
/// <see cref="EventRegistrationStatus.Confirmed"/>.
///
/// Idempotent — a registration that is already Confirmed / CheckedIn is left
/// alone so duplicate webhook deliveries remain safe.
/// </summary>
public sealed class ConfirmEventRegistrationOnPaymentSucceededHandler
    : INotificationHandler<PaymentSuccessfulEvent>
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<ConfirmEventRegistrationOnPaymentSucceededHandler> _logger;

    public ConfirmEventRegistrationOnPaymentSucceededHandler(
        IApplicationDbContext db,
        ILogger<ConfirmEventRegistrationOnPaymentSucceededHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Handle(PaymentSuccessfulEvent notification, CancellationToken cancellationToken)
    {
        if (notification.Purpose != PaymentPurpose.EventFee) return;
        if (notification.EventRegistrationId is not { } regId || regId == Guid.Empty) return;

        var reg = await _db.EventRegistrations
            .FirstOrDefaultAsync(r => r.Id == regId, cancellationToken);

        if (reg is null)
        {
            _logger.LogWarning(
                "Payment {PaymentId} claimed event registration {RegId} but no registration was found.",
                notification.PaymentId, regId);
            return;
        }

        if (reg.Status is EventRegistrationStatus.Confirmed
            or EventRegistrationStatus.CheckedIn)
        {
            return;
        }

        reg.Confirm(notification.PaymentId);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Confirmed event registration {RegId} after payment {PaymentId}.",
            regId, notification.PaymentId);
    }
}
