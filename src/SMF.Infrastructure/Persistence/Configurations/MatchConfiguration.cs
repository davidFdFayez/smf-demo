using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMF.Domain.Entities;
using SMF.Domain.Enums;

namespace SMF.Infrastructure.Persistence.Configurations;

internal sealed class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.ToTable("Matches");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Code)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(m => m.Code).IsUnique();

        builder.Property(m => m.HeadRefereeId).IsRequired();

        // Head referee must be a real Member. Role enforcement lives in the
        // command handler — SQL can't express "member role must be Referee".
        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(m => m.HeadRefereeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(m => m.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(m => m.ScheduledAtUtc).IsRequired();
        builder.Property(m => m.CreatedAtUtc).IsRequired();

        // Live streaming (PDF §9) — optional per-match broadcast.
        builder.Property(m => m.LiveStreamProvider)
            .HasConversion<string>()
            .HasMaxLength(24);
        builder.Property(m => m.LiveStreamUrl).HasMaxLength(500);

        // Phase 3 — round timer state owned by the timekeeper.
        builder.Property(m => m.TimekeeperId);
        builder.Property(m => m.CurrentRound);
        builder.Property(m => m.RoundDurationSeconds);
        builder.Property(m => m.RoundStartedAtUtc);
        builder.Property(m => m.RoundElapsedSecondsAtStart);
        builder.Property(m => m.IsTimerRunning);

        builder.OwnsMany(m => m.Referees, rb =>
        {
            rb.ToTable("MatchReferees");
            rb.WithOwner().HasForeignKey("MatchId");
            rb.HasKey("MatchId", nameof(MatchReferee.RefereeId));
            rb.Property(r => r.RefereeId).IsRequired();

            // Each referee assignment must point at a real Member.
            rb.HasOne<Member>()
                .WithMany()
                .HasForeignKey(r => r.RefereeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Navigation(m => m.Referees)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
