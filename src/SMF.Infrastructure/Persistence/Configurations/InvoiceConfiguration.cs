using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Persistence.Configurations;

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.InvoiceNumber).HasMaxLength(32).IsRequired();
        builder.HasIndex(i => i.InvoiceNumber).IsUnique();

        builder.Property(i => i.PaymentId).IsRequired();
        builder.HasIndex(i => i.PaymentId).IsUnique();

        builder.Property(i => i.MemberId);
        builder.HasIndex(i => i.MemberId);
        builder.Property(i => i.OrderId);
        builder.HasIndex(i => i.OrderId);

        builder.Property(i => i.BuyerName).HasMaxLength(200).IsRequired();
        builder.Property(i => i.BuyerEmail).HasMaxLength(320).IsRequired();
        builder.Property(i => i.BuyerTaxNumber).HasMaxLength(32);

        builder.Property(i => i.IssuerName).HasMaxLength(200).IsRequired();
        builder.Property(i => i.IssuerTaxNumber).HasMaxLength(32);
        builder.Property(i => i.IssuerAddress).HasMaxLength(512);

        builder.Property(i => i.IssuedAtUtc).IsRequired();
        builder.Property(i => i.SubtotalMinor).IsRequired();
        builder.Property(i => i.VatRateBp).IsRequired();
        builder.Property(i => i.VatAmountMinor).IsRequired();
        builder.Property(i => i.TotalMinor).IsRequired();
        builder.Property(i => i.Currency).HasMaxLength(3).IsFixedLength().IsRequired();

        builder.HasMany(i => i.Items)
            .WithOne()
            .HasForeignKey(li => li.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Invoice.Items))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class InvoiceLineItemConfiguration : IEntityTypeConfiguration<InvoiceLineItem>
{
    public void Configure(EntityTypeBuilder<InvoiceLineItem> builder)
    {
        builder.ToTable("InvoiceLineItems");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.InvoiceId).IsRequired();
        builder.Property(i => i.Description).HasMaxLength(512).IsRequired();
        builder.Property(i => i.Quantity).IsRequired();
        builder.Property(i => i.UnitPriceMinor).IsRequired();
    }
}
