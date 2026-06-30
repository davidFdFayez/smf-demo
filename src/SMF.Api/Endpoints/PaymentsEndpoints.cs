using MediatR;
using Microsoft.AspNetCore.Mvc;
using SMF.Application.Features.Payments.Commands.InitializePayment;
using SMF.Domain.Enums;

namespace SMF.Api.Endpoints;

/// <summary>
/// Minimal-API surface for the payment flow. The inbound provider webhook is
/// a full MVC controller (<c>PaymentCallbackController</c>) because it needs
/// raw-body access for signature verification; the outbound "start a
/// payment" call fits a minimal endpoint just fine.
/// </summary>
public static class PaymentsEndpoints
{
    public sealed record InitializePaymentRequest(
        Guid MemberId,
        PaymentProvider Provider,
        PaymentPurpose Purpose,
        long AmountMinor,
        string Currency,
        string CallbackUrl,
        string? Description = null,
        Guid? EventRegistrationId = null);

    public static void MapPaymentsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/payments").WithTags("Payments");

        group.MapPost("/", async (
            [FromBody] InitializePaymentRequest req,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new InitializePaymentCommand(
                req.MemberId,
                req.Provider,
                req.Purpose,
                req.AmountMinor,
                req.Currency,
                req.CallbackUrl,
                req.Description,
                req.EventRegistrationId), ct);

            return Results.Created($"/api/payments/{result.PaymentId}", result);
        })
        .WithName("InitializePayment")
        .WithSummary("Starts a payment attempt and returns the provider redirect URL.");
    }
}
