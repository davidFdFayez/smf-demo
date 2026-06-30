namespace SMF.Domain.Enums;

/// <summary>
/// Saudi-oriented payment rails that <see cref="SMF.Domain.Entities.Payment"/> supports.
/// Extend this enum (and plug in the corresponding IPaymentGatewayService
/// implementation) when adding new acquirers.
/// </summary>
public enum PaymentProvider
{
    Mada = 1,
    ApplePay = 2,
    Visa = 3
}
