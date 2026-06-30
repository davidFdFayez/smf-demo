using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Persistence.Configurations;

internal sealed class BroadcastCampaignConfiguration : IEntityTypeConfiguration<BroadcastCampaign>
{
    public void Configure(EntityTypeBuilder<BroadcastCampaign> builder)
    {
        builder.ToTable("BroadcastCampaigns");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Title).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Subject).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Body).IsRequired();

        // Channels stored as a string ("Email, Sms, Push") so the column is
        // legible in DB tools — the converter still round-trips the bitmask.
        builder.Property(c => c.Channels)
            .HasConversion<string>().HasMaxLength(64).IsRequired();

        builder.Property(c => c.Status)
            .HasConversion<string>().HasMaxLength(16).IsRequired();

        builder.Property(c => c.TargetRolesCsv).HasMaxLength(64);
        builder.Property(c => c.TargetClubId);
        builder.Property(c => c.TargetEventId);
        builder.Property(c => c.ActiveMembersOnly).IsRequired();

        builder.Property(c => c.TotalTargets).IsRequired();
        builder.Property(c => c.DeliveredCount).IsRequired();
        builder.Property(c => c.FailedCount).IsRequired();
        builder.Property(c => c.FailureReason).HasMaxLength(500);

        builder.Property(c => c.CreatedByMemberId);
        builder.Property(c => c.CreatedAtUtc).IsRequired();
        builder.Property(c => c.ScheduledAtUtc);
        builder.Property(c => c.StartedAtUtc);
        builder.Property(c => c.CompletedAtUtc);

        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => c.CreatedAtUtc);
    }
}
