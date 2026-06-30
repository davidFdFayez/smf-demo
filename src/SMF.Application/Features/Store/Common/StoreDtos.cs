using SMF.Domain.Enums;

namespace SMF.Application.Features.Store.Common;

public sealed record CategoryDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    string? ImageUrl,
    int DisplayOrder,
    bool IsActive);

public sealed record ProductSummaryDto(
    Guid Id,
    string Sku,
    string Name,
    string? ImageUrl,
    long PriceMinor,
    string Currency,
    int StockOnHand,
    bool IsActive,
    bool IsLowStock,
    Guid CategoryId,
    string CategoryName);

public sealed record ProductDetailDto(
    Guid Id,
    string Sku,
    string Name,
    string? Description,
    string? ImageUrl,
    long PriceMinor,
    string Currency,
    int StockOnHand,
    int? LowStockThreshold,
    bool IsActive,
    Guid CategoryId,
    string CategoryName,
    string CategorySlug);

public sealed record CartLineDto(
    Guid ProductId,
    string ProductSku,
    string ProductName,
    string? ImageUrl,
    long UnitPriceMinor,
    int Quantity,
    long LineTotalMinor);

public sealed record CartDto(
    Guid Id,
    Guid? GuestKey,
    Guid? MemberId,
    string Currency,
    long SubtotalMinor,
    int VatRateBp,
    long EstimatedVatMinor,
    long EstimatedTotalMinor,
    IReadOnlyCollection<CartLineDto> Items);

public sealed record OrderLineDto(
    Guid ProductId,
    string ProductSku,
    string ProductName,
    long UnitPriceMinor,
    int Quantity,
    long LineTotalMinor);

public sealed record OrderShippingAddressDto(
    string RecipientName,
    string Line1,
    string? Line2,
    string City,
    string Region,
    string PostalCode,
    string Country,
    string PhoneNumber);

public sealed record OrderDto(
    Guid Id,
    string OrderNumber,
    Guid? MemberId,
    string BuyerName,
    string BuyerEmail,
    OrderShippingAddressDto ShippingAddress,
    long SubtotalMinor,
    int VatRateBp,
    long VatAmountMinor,
    long ShippingFeeMinor,
    long TotalMinor,
    string Currency,
    OrderStatus Status,
    Guid? PaymentId,
    Guid? InvoiceId,
    DateTime CreatedAtUtc,
    DateTime? PaidAtUtc,
    IReadOnlyCollection<OrderLineDto> Items);

public sealed record CheckoutResultDto(
    Guid OrderId,
    string OrderNumber,
    Guid PaymentId,
    string ProviderTransactionId,
    string RedirectUrl,
    long TotalMinor,
    string Currency);
