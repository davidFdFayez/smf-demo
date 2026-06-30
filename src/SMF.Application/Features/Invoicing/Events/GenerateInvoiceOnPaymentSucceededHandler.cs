using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMF.Application.Common.Interfaces;
using SMF.Application.Features.Payments.Events;
using SMF.Domain.Entities;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Invoicing.Events;

/// <summary>
/// Subscribes to <see cref="PaymentSuccessfulEvent"/> and produces an
/// <see cref="Invoice"/>. Behaviour per purpose:
///
///   * <see cref="PaymentPurpose.ProductPurchase"/>: looks up the linked
///     <see cref="Order"/>, marks it Paid, and snapshots its line items
///     onto the invoice.
///   * <see cref="PaymentPurpose.MembershipFee"/> and
///     <see cref="PaymentPurpose.EventFee"/>: emits a single-line invoice
///     with a generic description ("SMF Annual Membership" / "Event entry
///     fee — {event title}"). Both already have other handlers driving
///     side effects (member activation / registration confirmation) — this
///     one just adds the audit-grade billing record.
///
/// Idempotent — duplicate webhook deliveries surface a uniqueness conflict
/// on (PaymentId) which we swallow as a no-op.
/// </summary>
public sealed class GenerateInvoiceOnPaymentSucceededHandler
    : INotificationHandler<PaymentSuccessfulEvent>
{
    private readonly IApplicationDbContext _db;
    private readonly IBillingSequenceRepository _sequences;
    private readonly IInvoicingSettings _settings;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<GenerateInvoiceOnPaymentSucceededHandler> _logger;

    public GenerateInvoiceOnPaymentSucceededHandler(
        IApplicationDbContext db,
        IBillingSequenceRepository sequences,
        IInvoicingSettings settings,
        IDateTimeProvider clock,
        ILogger<GenerateInvoiceOnPaymentSucceededHandler> logger)
    {
        _db = db;
        _sequences = sequences;
        _settings = settings;
        _clock = clock;
        _logger = logger;
    }

    public async Task Handle(PaymentSuccessfulEvent notification, CancellationToken cancellationToken)
    {
        var existing = await _db.Invoices
            .AnyAsync(i => i.PaymentId == notification.PaymentId, cancellationToken);
        if (existing)
        {
            _logger.LogDebug(
                "Invoice for payment {PaymentId} already exists — skipping.", notification.PaymentId);
            return;
        }

        var year = _clock.Today.Year;
        var seq = await _sequences.NextAsync($"INVOICE:{year}", cancellationToken);
        var invoiceNumber = $"INV-{year:D4}-{seq:D6}";

        Invoice invoice;
        switch (notification.Purpose)
        {
            case PaymentPurpose.ProductPurchase:
                invoice = await BuildOrderInvoice(notification, invoiceNumber, cancellationToken);
                break;
            case PaymentPurpose.MembershipFee:
                invoice = await BuildMembershipInvoice(notification, invoiceNumber, cancellationToken);
                break;
            case PaymentPurpose.EventFee:
                invoice = await BuildEventInvoice(notification, invoiceNumber, cancellationToken);
                break;
            default:
                _logger.LogWarning(
                    "No invoice template for payment purpose {Purpose}; payment {PaymentId} skipped.",
                    notification.Purpose, notification.PaymentId);
                return;
        }

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Issued invoice {InvoiceNumber} (id={InvoiceId}) for payment {PaymentId}, total {Total} {Currency}.",
            invoice.InvoiceNumber, invoice.Id, notification.PaymentId,
            invoice.TotalMinor, invoice.Currency);
    }

    private async Task<Invoice> BuildOrderInvoice(
        PaymentSuccessfulEvent n, string invoiceNumber, CancellationToken ct)
    {
        var order = await _db.Orders.Include(o => o.Items)
                       .FirstOrDefaultAsync(o => o.PaymentId == n.PaymentId, ct)
                   ?? throw new InvalidOperationException(
                       $"Product purchase payment {n.PaymentId} has no linked order.");

        // Idempotent state transition — duplicate event deliveries are safe.
        if (order.MarkPaid(_clock.UtcNow))
        {
            // Order moves Paid; explicit save is unnecessary here because
            // _db.SaveChangesAsync below covers everything in one tx.
        }

        var items = order.Items
            .Select(i => InvoiceLineItem.Create(
                $"{i.ProductSku} — {i.ProductName} × {i.Quantity}",
                i.Quantity,
                i.UnitPriceMinor))
            .ToList();

        return Invoice.Create(
            invoiceNumber,
            n.PaymentId,
            order.MemberId,
            order.Id,
            order.BuyerName,
            order.BuyerEmail,
            buyerTaxNumber: null,
            _settings.IssuerName,
            _settings.IssuerTaxNumber,
            _settings.IssuerAddress,
            items,
            order.VatRateBp,
            order.Currency,
            _clock.UtcNow);
    }

    private async Task<Invoice> BuildMembershipInvoice(
        PaymentSuccessfulEvent n, string invoiceNumber, CancellationToken ct)
    {
        var member = n.MemberId is { } id
            ? await _db.Members.FirstOrDefaultAsync(m => m.Id == id, ct)
            : null;

        var buyerName = member?.FullName ?? "SMF Member";
        var buyerEmail = member?.Email ?? "no-reply@smf.local";

        var line = InvoiceLineItem.Create(
            description: $"SMF Annual Membership — {member?.SMF_ID ?? n.MemberId?.ToString() ?? "(member)"}",
            quantity: 1,
            unitPriceMinor: n.AmountMinor);

        return Invoice.Create(
            invoiceNumber,
            n.PaymentId,
            member?.Id,
            orderId: null,
            buyerName,
            buyerEmail,
            buyerTaxNumber: null,
            _settings.IssuerName,
            _settings.IssuerTaxNumber,
            _settings.IssuerAddress,
            new[] { line },
            _settings.DefaultVatRateBp,
            n.Currency,
            _clock.UtcNow);
    }

    private async Task<Invoice> BuildEventInvoice(
        PaymentSuccessfulEvent n, string invoiceNumber, CancellationToken ct)
    {
        var member = n.MemberId is { } mid
            ? await _db.Members.FirstOrDefaultAsync(m => m.Id == mid, ct)
            : null;

        var eventTitle = "Federation Event";
        if (n.EventRegistrationId is { } regId)
        {
            var regAndEvent = await (
                from r in _db.EventRegistrations
                join e in _db.Events on r.EventId equals e.Id
                where r.Id == regId
                select new { e.Title }).FirstOrDefaultAsync(ct);
            if (regAndEvent is not null) eventTitle = regAndEvent.Title;
        }

        var buyerName = member?.FullName ?? "SMF Member";
        var buyerEmail = member?.Email ?? "no-reply@smf.local";

        var line = InvoiceLineItem.Create(
            description: $"Event entry fee — {eventTitle}",
            quantity: 1,
            unitPriceMinor: n.AmountMinor);

        return Invoice.Create(
            invoiceNumber,
            n.PaymentId,
            member?.Id,
            orderId: null,
            buyerName,
            buyerEmail,
            buyerTaxNumber: null,
            _settings.IssuerName,
            _settings.IssuerTaxNumber,
            _settings.IssuerAddress,
            new[] { line },
            _settings.DefaultVatRateBp,
            n.Currency,
            _clock.UtcNow);
    }
}
