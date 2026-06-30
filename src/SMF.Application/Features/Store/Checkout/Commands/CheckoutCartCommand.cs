using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;
using SMF.Application.Features.Store.Cart.Common;
using SMF.Application.Features.Store.Cart.Queries;
using SMF.Application.Features.Store.Common;
using SMF.Domain.Entities;
using SMF.Domain.Enums;
using SMF.Domain.ValueObjects;

namespace SMF.Application.Features.Store.Checkout.Commands;

/// <summary>
/// Atomically convert a cart into an <see cref="Order"/> + initialise a
/// <see cref="Payment"/> at the gateway:
///
///   1. Reload the cart with its items.
///   2. Reserve stock on every product (decrement, throws on shortage).
///   3. Allocate the next yearly order number.
///   4. Build the <see cref="Order"/> aggregate (snapshots prices, computes VAT).
///   5. Initialise the gateway, persist a Pending <see cref="Payment"/>.
///   6. Attach the payment id to the order, clear the cart.
///   7. Commit the lot in a single <see cref="IApplicationDbContext.SaveChangesAsync"/>.
///
/// On <see cref="DbUpdateConcurrencyException"/> from the product rowversion
/// guard we retry the whole thing up to <see cref="MaxRetries"/> times — that
/// covers two simultaneous orders fighting over the same last-in-stock unit.
/// </summary>
public sealed record CheckoutCartCommand(
    CartIdentity Identity,
    string BuyerName,
    string BuyerEmail,
    string? BuyerTaxNumber,
    OrderShippingAddressDto ShippingAddress,
    long ShippingFeeMinor,
    PaymentProvider Provider,
    string CallbackUrl,
    Guid? MemberId = null) : IRequest<CheckoutResultDto>;

public sealed class CheckoutCartCommandValidator : AbstractValidator<CheckoutCartCommand>
{
    public CheckoutCartCommandValidator()
    {
        RuleFor(x => x.Identity).Must(i => i.IsValid)
            .WithMessage("Cart identity must include either a guest key or a member id.");
        RuleFor(x => x.BuyerName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BuyerEmail).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.BuyerTaxNumber).MaximumLength(32);
        RuleFor(x => x.ShippingAddress).NotNull();
        RuleFor(x => x.ShippingAddress.RecipientName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ShippingAddress.Line1).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ShippingAddress.City).NotEmpty().MaximumLength(120);
        RuleFor(x => x.ShippingAddress.Region).NotEmpty().MaximumLength(120);
        RuleFor(x => x.ShippingAddress.PostalCode).NotEmpty().MaximumLength(20);
        RuleFor(x => x.ShippingAddress.Country).NotEmpty().Length(2);
        RuleFor(x => x.ShippingAddress.PhoneNumber).NotEmpty().MaximumLength(32);
        RuleFor(x => x.ShippingFeeMinor).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Provider).IsInEnum();
        RuleFor(x => x.CallbackUrl).NotEmpty().Must(IsAbsoluteUri)
            .WithMessage("CallbackUrl must be an absolute URL.");
    }

    private static bool IsAbsoluteUri(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out _);
}

public sealed class CheckoutCartCommandHandler : IRequestHandler<CheckoutCartCommand, CheckoutResultDto>
{
    private const int MaxRetries = 3;

    private readonly IApplicationDbContext _db;
    private readonly IPaymentRepository _payments;
    private readonly IPaymentGatewayService _gateway;
    private readonly IBillingSequenceRepository _sequences;
    private readonly IInvoicingSettings _settings;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<CheckoutCartCommandHandler> _logger;

    public CheckoutCartCommandHandler(
        IApplicationDbContext db,
        IPaymentRepository payments,
        IPaymentGatewayService gateway,
        IBillingSequenceRepository sequences,
        IInvoicingSettings settings,
        IDateTimeProvider clock,
        ILogger<CheckoutCartCommandHandler> logger)
    {
        _db = db;
        _payments = payments;
        _gateway = gateway;
        _sequences = sequences;
        _settings = settings;
        _clock = clock;
        _logger = logger;
    }

    public async Task<CheckoutResultDto> Handle(
        CheckoutCartCommand request, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return await TryCheckout(request, cancellationToken);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxRetries)
            {
                _logger.LogWarning(
                    "Checkout race detected for cart {Identity} on attempt {Attempt} — retrying.",
                    request.Identity, attempt);
            }
        }
        throw new InvalidOperationException(
            "Checkout failed after multiple concurrency conflicts; please try again.");
    }

    private async Task<CheckoutResultDto> TryCheckout(
        CheckoutCartCommand request, CancellationToken cancellationToken)
    {
        var cart = await CartLookup.LoadAsync(_db, request.Identity, cancellationToken)
                   ?? throw new NotFoundException("Cart", request.Identity);
        if (cart.Items.Count == 0)
            throw new InvalidOperationException("Cannot check out an empty cart.");

        var productIds = cart.Items.Select(i => i.ProductId).ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        foreach (var line in cart.Items)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
                throw new NotFoundException("Product", line.ProductId);
            product.ReserveStock(line.Quantity, _clock.UtcNow);
        }

        var year = _clock.Today.Year;
        var seq = await _sequences.NextAsync($"ORDER:{year}", cancellationToken);
        var orderNumber = $"ORD-{year:D4}-{seq:D5}";

        var address = ShippingAddress.Create(
            request.ShippingAddress.RecipientName,
            request.ShippingAddress.Line1,
            request.ShippingAddress.Line2,
            request.ShippingAddress.City,
            request.ShippingAddress.Region,
            request.ShippingAddress.PostalCode,
            request.ShippingAddress.Country,
            request.ShippingAddress.PhoneNumber);

        var order = Order.CreateFromCart(
            orderNumber,
            request.MemberId ?? request.Identity.MemberId,
            request.BuyerName,
            request.BuyerEmail,
            address,
            cart.Items,
            _settings.DefaultVatRateBp,
            request.ShippingFeeMinor,
            cart.Currency,
            _clock.UtcNow);

        _db.Orders.Add(order);

        // For guest checkouts no SMF member exists; pass null down to the
        // gateway and leave Payment.MemberId null too. Member-bound checkouts
        // forward the linked member so existing payment-history queries still
        // see the order.
        var memberIdForPayment = request.MemberId ?? request.Identity.MemberId;

        var gw = await _gateway.InitializePayment(
            new PaymentRequest(
                memberIdForPayment ?? Guid.Empty,
                request.Provider,
                PaymentPurpose.ProductPurchase,
                order.TotalMinor,
                order.Currency,
                request.CallbackUrl,
                $"Order {order.OrderNumber}"),
            cancellationToken);

        var payment = Payment.Initiate(
            memberIdForPayment,
            request.Provider,
            PaymentPurpose.ProductPurchase,
            gw.TransactionId,
            order.TotalMinor,
            order.Currency,
            _clock.UtcNow);

        await _payments.AddAsync(payment, cancellationToken);

        order.AttachPayment(payment.Id, _clock.UtcNow);
        cart.Clear(_clock.UtcNow);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Checkout: order {OrderNumber} for {Total} {Currency} (cart={CartId}, payment={PaymentId}, txn={Txn}).",
            order.OrderNumber, order.TotalMinor, order.Currency, cart.Id, payment.Id, gw.TransactionId);

        return new CheckoutResultDto(
            order.Id, order.OrderNumber, payment.Id,
            gw.TransactionId, gw.RedirectUrl, order.TotalMinor, order.Currency);
    }
}
