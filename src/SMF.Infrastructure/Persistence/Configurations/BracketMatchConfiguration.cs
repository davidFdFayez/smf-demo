using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Persistence.Configurations;

internal sealed class BracketMatchConfiguration : IEntityTypeConfiguration<BracketMatch>
{
    public void Configure(EntityTypeBuilder<BracketMatch> builder)
    {
        builder.ToTable("BracketMatches");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.TournamentId).IsRequired();
        builder.Property(x => x.Round).IsRequired();
        builder.Property(x => x.OrderInRound).IsRequired();

        builder.Property(x => x.ParticipantAId);
        builder.Property(x => x.ParticipantBId);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Winner);
        builder.Property(x => x.ParentMatchAId);
        builder.Property(x => x.ParentMatchBId);

        builder.HasIndex(x => new { x.TournamentId, x.Round, x.OrderInRound });
    }
}
