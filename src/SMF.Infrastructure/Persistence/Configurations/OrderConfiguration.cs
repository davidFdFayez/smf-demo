using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.OrderNumber).HasMaxLength(32).IsRequired();
        builder.HasIndex(o => o.OrderNumber).IsUnique();

        builder.Property(o => o.MemberId);
        builder.HasIndex(o => o.MemberId);
        builder.Property(o => o.PaymentId);
        builder.HasIndex(o => o.PaymentId).IsUnique()
            .HasFilter("[PaymentId] IS NOT NULL");

        builder.Property(o => o.BuyerName).HasMaxLength(200).IsRequired();
        builder.Property(o => o.BuyerEmail).HasMaxLength(320).IsRequired();

        builder.Property(o => o.SubtotalMinor).IsRequired();
        builder.Property(o => o.VatRateBp).IsRequired();
        builder.Property(o => o.VatAmountMinor).IsRequired();
        builder.Property(o => o.ShippingFeeMinor).IsRequired();
        builder.Property(o => o.TotalMinor).IsRequired();
        builder.Property(o => o.Currency).HasMaxLength(3).IsFixedLength().IsRequired();

        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(o => o.CreatedAtUtc).IsRequired();
        builder.Property(o => o.UpdatedAtUtc).IsRequired();
        builder.Property(o => o.CancellationReason).HasMaxLength(512);

        // Owned shipping address — flattens to columns on the Orders table.
        builder.OwnsOne(o => o.ShippingAddress, ship =>
        {
            ship.Property(s => s.RecipientName).HasMaxLength(200).IsRequired().HasColumnName("Ship_RecipientName");
            ship.Property(s => s.Line1).HasMaxLength(200).IsRequired().HasColumnName("Ship_Line1");
            ship.Property(s => s.Line2).HasMaxLength(200).HasColumnName("Ship_Line2");
            ship.Property(s => s.City).HasMaxLength(120).IsRequired().HasColumnName("Ship_City");
            ship.Property(s => s.Region).HasMaxLength(120).IsRequired().HasColumnName("Ship_Region");
            ship.Property(s => s.PostalCode).HasMaxLength(20).IsRequired().HasColumnName("Ship_PostalCode");
            ship.Property(s => s.Country).HasMaxLength(2).IsFixedLength().IsRequired().HasColumnName("Ship_Country");
            ship.Property(s => s.PhoneNumber).HasMaxLength(32).IsRequired().HasColumnName("Ship_PhoneNumber");
        });

        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Order.Items))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.OrderId).IsRequired();
        builder.Property(i => i.ProductId).IsRequired();
        builder.Property(i => i.ProductSku).HasMaxLength(64).IsRequired();
        builder.Property(i => i.ProductName).HasMaxLength(200).IsRequired();
        builder.Property(i => i.UnitPriceMinor).IsRequired();
        builder.Property(i => i.Quantity).IsRequired();
        builder.HasIndex(i => i.ProductId);
    }
}
