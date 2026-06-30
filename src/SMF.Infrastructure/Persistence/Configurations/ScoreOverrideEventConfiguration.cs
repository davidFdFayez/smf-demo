using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Persistence.Configurations;

internal sealed class ScoreOverrideEventConfiguration : IEntityTypeConfiguration<ScoreOverrideEvent>
{
    public void Configure(EntityTypeBuilder<ScoreOverrideEvent> builder)
    {
        builder.ToTable("ScoreOverrideEvents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.MatchId).IsRequired();
        builder.Property(e => e.HeadRefereeId).IsRequired();
        builder.Property(e => e.Red).IsRequired();
        builder.Property(e => e.Blue).IsRequired();
        builder.Property(e => e.OccurredAtUtc).IsRequired();

        builder.HasIndex(e => new { e.MatchId, e.OccurredAtUtc });
    }
}
