using System.Text.Json;
using System.Text.Json.Serialization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMF.Application.Common.Exceptions;
using SMF.Application.Features.Payments.Commands.ConfirmPaymentCallback;
using SMF.Infrastructure.Payments;

namespace SMF.Api.Controllers;

/// <summary>
/// Webhook endpoint the payment provider (MADA / Apple Pay sandbox) POSTs to
/// after a transaction reaches a terminal state.
///
/// Security model:
///   * <see cref="AllowAnonymousAttribute"/> — providers don't carry our JWT.
///   * HMAC-SHA256 signature of the raw body, verified via
///     <see cref="WebhookSignatureVerifier"/>. Any mismatch is a 400.
///   * The handler re-verifies the transaction state against the provider's
///     API — the webhook body is never trusted on its own.
///   * Idempotent: duplicate deliveries resolve to the same Payment aggregate
///     and short-circuit before the PaymentSuccessfulEvent is re-published.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/payments/callback")]
public sealed class PaymentCallbackController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IMediator _mediator;
    private readonly WebhookSignatureVerifier _signatures;
    private readonly ILogger<PaymentCallbackController> _logger;

    public PaymentCallbackController(
        IMediator mediator,
        WebhookSignatureVerifier signatures,
        ILogger<PaymentCallbackController> logger)
    {
        _mediator = mediator;
        _signatures = signatures;
        _logger = logger;
    }

    /// <summary>
    /// Accepts the provider's callback and drives the payment state machine.
    ///
    /// Expected request shape (camelCase JSON):
    /// <code>
    /// { "transactionId": "SBX-MADA-…", "status": "Succeeded", "reason": null }
    /// </code>
    ///
    /// The <c>status</c> field is informational only — the handler asks the
    /// gateway for the authoritative state.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Callback(CancellationToken cancellationToken)
    {
        // 1. Buffer + read the raw body so we can HMAC it AND parse it.
        Request.EnableBuffering();
        string rawBody;
        using (var reader = new StreamReader(
                   Request.Body,
                   encoding: System.Text.Encoding.UTF8,
                   detectEncodingFromByteOrderMarks: false,
                   leaveOpen: true))
        {
            rawBody = await reader.ReadToEndAsync(cancellationToken);
            Request.Body.Position = 0;
        }

        // 2. Signature check. Missing / mismatched → reject with 400.
        var providedSignature = Request.Headers[_signatures.HeaderName].ToString();
        if (!_signatures.Verify(rawBody, providedSignature))
        {
            _logger.LogWarning(
                "Rejected payment callback: invalid or missing '{Header}' signature.",
                _signatures.HeaderName);
            return BadRequest(new { error = "Invalid signature." });
        }

        // 3. Parse just enough of the body to route the command. The Mada
        //    sandbox uses 'transactionId', Tap Payments uses 'id' on the
        //    charge envelope — accept either so the same endpoint serves
        //    both providers without configuration gymnastics.
        CallbackPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<CallbackPayload>(rawBody, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Rejected payment callback: malformed JSON body.");
            return BadRequest(new { error = "Malformed JSON." });
        }

        var transactionId = payload?.TransactionId ?? payload?.Id;
        if (string.IsNullOrWhiteSpace(transactionId))
            return BadRequest(new { error = "transactionId (or id) is required." });

        // 4. Drive the state machine through MediatR.
        try
        {
            var result = await _mediator.Send(
                new ConfirmPaymentCallbackCommand(transactionId),
                cancellationToken);

            _logger.LogInformation(
                "Processed payment callback for {Txn}: status={Status}, transitioned={Transitioned}.",
                transactionId, result.Status, result.TransitionedNow);

            return Ok(new
            {
                paymentId = result.PaymentId,
                status = result.Status.ToString(),
                transitioned = result.TransitionedNow
            });
        }
        catch (NotFoundException nf)
        {
            _logger.LogWarning(nf,
                "Callback for unknown transaction {Txn}; returning 404 so the provider stops retrying.",
                transactionId);
            return NotFound(new { error = nf.Message });
        }
    }

    /// <summary>
    /// Webhook envelope. <c>transactionId</c> is the Mada sandbox shape;
    /// <c>id</c> is what Tap Payments sends (full charge object). Either is
    /// resolved into the canonical transaction id before dispatch.
    /// </summary>
    private sealed record CallbackPayload(
        string? TransactionId = null,
        string? Id = null,
        string? Status = null,
        string? Reason = null);
}
