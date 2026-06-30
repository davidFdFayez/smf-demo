using SMF.Domain.Entities;

namespace SMF.Application.Common.Interfaces;

public interface IPaymentRepository
{
    Task AddAsync(Payment payment, CancellationToken cancellationToken = default);

    Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up a payment by the gateway's transaction id. Used by the webhook
    /// handler to drive idempotency — repeated deliveries resolve to the same
    /// aggregate and hit the no-op branch on <see cref="Payment.MarkSucceeded"/>.
    /// </summary>
    Task<Payment?> GetByProviderTransactionIdAsync(
        string providerTransactionId,
        CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
