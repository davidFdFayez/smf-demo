using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Persistence.Configurations;

internal sealed class StrikeEventConfiguration : IEntityTypeConfiguration<StrikeEvent>
{
    public void Configure(EntityTypeBuilder<StrikeEvent> builder)
    {
        builder.ToTable("StrikeEvents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.MatchId).IsRequired();
        builder.Property(e => e.RefereeId).IsRequired();
        builder.Property(e => e.FighterColor)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(e => e.OccurredAtUtc).IsRequired();

        // Tight index on (MatchId, OccurredAtUtc) for fast replay in timestamp order.
        builder.HasIndex(e => new { e.MatchId, e.OccurredAtUtc });
    }
}
