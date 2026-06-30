using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Channel)
            .HasConversion<string>().HasMaxLength(16).IsRequired();

        builder.Property(n => n.RecipientAddress).HasMaxLength(2048).IsRequired();
        builder.Property(n => n.Subject).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Body).IsRequired();

        builder.Property(n => n.Status)
            .HasConversion<string>().HasMaxLength(16).IsRequired();

        builder.Property(n => n.AttemptCount).IsRequired();
        builder.Property(n => n.LastError).HasMaxLength(1000);
        builder.Property(n => n.ProviderMessageId).HasMaxLength(256);

        builder.Property(n => n.CreatedAtUtc).IsRequired();
        builder.Property(n => n.SentAtUtc);

        builder.HasIndex(n => n.Status);
        builder.HasIndex(n => n.MemberId);
        builder.HasIndex(n => n.BroadcastCampaignId);
        builder.HasIndex(n => n.CreatedAtUtc);
    }
}
