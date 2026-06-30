using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(p => p.Id);

        // Optional FK — null for guest e-commerce checkouts. Restrict on
        // delete so we never lose a payment row by removing its member.
        builder.Property(p => p.MemberId).IsRequired(false);
        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(p => p.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.Provider)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(p => p.Purpose)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(p => p.ProviderTransactionId)
            .HasMaxLength(128)
            .IsRequired();

        // Provider + transaction id together must be unique — prevents a
        // malicious caller from colliding with another provider's txn id.
        builder.HasIndex(p => new { p.Provider, p.ProviderTransactionId })
            .IsUnique();

        builder.Property(p => p.AmountMinor).IsRequired();
        builder.Property(p => p.Currency).HasMaxLength(3).IsRequired().IsFixedLength();

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(p => p.CreatedAtUtc).IsRequired();
        builder.Property(p => p.CompletedAtUtc);
        builder.Property(p => p.FailureReason).HasMaxLength(512);

        // Event-fee correlation — lets the webhook handler confirm the exact
        // registration that triggered the payment without guessing.
        builder.Property(p => p.EventRegistrationId);
        builder.HasIndex(p => p.EventRegistrationId);

        builder.HasIndex(p => p.MemberId);
    }
}
