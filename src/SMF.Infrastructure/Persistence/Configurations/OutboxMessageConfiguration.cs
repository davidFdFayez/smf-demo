using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Type)
            .HasMaxLength(512)
            .IsRequired();

        // Payload may be large (multi-KB events) — map to nvarchar(max) on SQL Server.
        builder.Property(m => m.Payload)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(m => m.OccurredAtUtc).IsRequired();
        builder.Property(m => m.ProcessedAtUtc);
        builder.Property(m => m.Attempts).IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(2048);

        // Poller query is "unprocessed, ordered by OccurredAtUtc" — index it.
        builder.HasIndex(m => new { m.ProcessedAtUtc, m.OccurredAtUtc })
            .HasDatabaseName("IX_OutboxMessages_Unprocessed");
    }
}
