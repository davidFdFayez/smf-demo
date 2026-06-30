using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Persistence.Repositories;

internal sealed class PaymentRepository : IPaymentRepository
{
    private readonly ApplicationDbContext _db;

    public PaymentRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(Payment payment, CancellationToken cancellationToken = default)
        => _db.Payments.AddAsync(payment, cancellationToken).AsTask();

    public Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.Payments.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<Payment?> GetByProviderTransactionIdAsync(
        string providerTransactionId,
        CancellationToken cancellationToken = default)
        => _db.Payments.FirstOrDefaultAsync(
            p => p.ProviderTransactionId == providerTransactionId, cancellationToken);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}
