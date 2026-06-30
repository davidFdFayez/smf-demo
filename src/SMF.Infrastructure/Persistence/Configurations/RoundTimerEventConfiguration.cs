using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Persistence.Configurations;

internal sealed class RoundTimerEventConfiguration : IEntityTypeConfiguration<RoundTimerEvent>
{
    public void Configure(EntityTypeBuilder<RoundTimerEvent> builder)
    {
        builder.ToTable("RoundTimerEvents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.MatchId).IsRequired();
        builder.Property(e => e.TimekeeperId).IsRequired();

        builder.Property(e => e.Action)
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();

        builder.Property(e => e.RoundNumber).IsRequired();
        builder.Property(e => e.RoundDurationSeconds).IsRequired();
        builder.Property(e => e.ElapsedSecondsAtEvent).IsRequired();
        builder.Property(e => e.OccurredAtUtc).IsRequired();

        // Replay ordering by match — the hottest query pattern.
        builder.HasIndex(e => new { e.MatchId, e.OccurredAtUtc });
    }
}
