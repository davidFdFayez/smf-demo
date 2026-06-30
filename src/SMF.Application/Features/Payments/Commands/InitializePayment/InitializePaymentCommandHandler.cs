using MediatR;
using Microsoft.Extensions.Logging;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Entities;

namespace SMF.Application.Features.Payments.Commands.InitializePayment;

public sealed class InitializePaymentCommandHandler
    : IRequestHandler<InitializePaymentCommand, InitializePaymentResult>
{
    private readonly IPaymentRepository _payments;
    private readonly IMemberRepository _members;
    private readonly IPaymentGatewayService _gateway;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<InitializePaymentCommandHandler> _logger;

    public InitializePaymentCommandHandler(
        IPaymentRepository payments,
        IMemberRepository members,
        IPaymentGatewayService gateway,
        IDateTimeProvider clock,
        ILogger<InitializePaymentCommandHandler> logger)
    {
        _payments = payments;
        _members = members;
        _gateway = gateway;
        _clock = clock;
        _logger = logger;
    }

    public async Task<InitializePaymentResult> Handle(
        InitializePaymentCommand request,
        CancellationToken cancellationToken)
    {
        // Guard: we won't spin up a checkout for a member that doesn't exist.
        // This is also what turns bogus frontend input into a clean 404.
        var member = await _members.GetByIdAsync(request.MemberId, cancellationToken)
                     ?? throw new NotFoundException("Member", request.MemberId);

        var gatewayResult = await _gateway.InitializePayment(
            new PaymentRequest(
                member.Id,
                request.Provider,
                request.Purpose,
                request.AmountMinor,
                request.Currency,
                request.CallbackUrl,
                request.Description),
            cancellationToken);

        var payment = Payment.Initiate(
            member.Id,
            request.Provider,
            request.Purpose,
            gatewayResult.TransactionId,
            request.AmountMinor,
            request.Currency,
            _clock.UtcNow);

        if (request.EventRegistrationId is { } regId && regId != Guid.Empty)
            payment.AttachEventRegistration(regId);

        await _payments.AddAsync(payment, cancellationToken);
        await _payments.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Initialized {Provider} {Purpose} payment {PaymentId} for member {MemberId} (txn {Txn}, {Amount} {Currency}).",
            request.Provider,
            request.Purpose,
            payment.Id,
            member.Id,
            gatewayResult.TransactionId,
            request.AmountMinor,
            request.Currency);

        return new InitializePaymentResult(
            payment.Id,
            gatewayResult.TransactionId,
            gatewayResult.RedirectUrl);
    }
}
