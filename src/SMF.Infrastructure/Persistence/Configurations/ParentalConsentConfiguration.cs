using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Persistence.Configurations;

public sealed class ParentalConsentConfiguration : IEntityTypeConfiguration<ParentalConsent>
{
    public void Configure(EntityTypeBuilder<ParentalConsent> b)
    {
        b.ToTable("ParentalConsents");
        b.HasKey(x => x.Id);

        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.GuardianFullName).IsRequired().HasMaxLength(200);
        b.Property(x => x.Relation).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.GuardianEmail).IsRequired().HasMaxLength(200);
        b.Property(x => x.GuardianPhone).IsRequired().HasMaxLength(32);
        b.Property(x => x.GuardianNationalId).HasMaxLength(64);
        b.Property(x => x.TokenHash).IsRequired().HasMaxLength(64);
        b.Property(x => x.PolicyVersion).HasMaxLength(40);
        b.Property(x => x.DecisionIpAddress).HasMaxLength(64);
        b.Property(x => x.DecisionUserAgent).HasMaxLength(400);
        b.Property(x => x.DecisionSignatureHmac).HasMaxLength(128);
        b.Property(x => x.DeclineReason).HasMaxLength(2000);

        b.HasIndex(x => x.MemberId);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.TokenHash).IsUnique();

        b.HasOne<Member>().WithMany()
            .HasForeignKey(x => x.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne<PolicyDocument>().WithMany()
            .HasForeignKey(x => x.PolicyDocumentId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}

public sealed class ConsentLogConfiguration : IEntityTypeConfiguration<ConsentLog>
{
    public void Configure(EntityTypeBuilder<ConsentLog> b)
    {
        b.ToTable("ConsentLogs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();

        b.Property(x => x.EventType).HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.PolicyKind).HasMaxLength(40);
        b.Property(x => x.PolicyVersion).HasMaxLength(40);
        b.Property(x => x.ContentHash).HasMaxLength(64);
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.UserAgent).HasMaxLength(400);
        b.Property(x => x.DetailsJson).HasMaxLength(2000);
        b.Property(x => x.SignatureHmac).HasMaxLength(128);

        b.HasIndex(x => x.OccurredAtUtc);
        b.HasIndex(x => x.MemberId);
        b.HasIndex(x => x.ParentalConsentId);
        b.HasIndex(x => x.EventType);
    }
}
