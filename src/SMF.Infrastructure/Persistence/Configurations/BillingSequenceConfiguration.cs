using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMF.Infrastructure.Persistence.Entities;

namespace SMF.Infrastructure.Persistence.Configurations;

internal sealed class BillingSequenceConfiguration : IEntityTypeConfiguration<BillingSequence>
{
    public void Configure(EntityTypeBuilder<BillingSequence> builder)
    {
        builder.ToTable("BillingSequences");
        builder.HasKey(s => s.Key);
        builder.Property(s => s.Key).HasMaxLength(64).IsRequired();
        builder.Property(s => s.LastSequence).IsRequired();
        builder.Property(s => s.RowVersion).IsRowVersion().IsConcurrencyToken();
    }
}
