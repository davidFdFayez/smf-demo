using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Persistence.Configurations;

public sealed class PolicyDocumentConfiguration : IEntityTypeConfiguration<PolicyDocument>
{
    public void Configure(EntityTypeBuilder<PolicyDocument> b)
    {
        b.ToTable("PolicyDocuments");
        b.HasKey(x => x.Id);

        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.Version).IsRequired().HasMaxLength(40);
        b.Property(x => x.Title).IsRequired().HasMaxLength(200);
        b.Property(x => x.BodyMarkdown).IsRequired();
        b.Property(x => x.ContentHash).IsRequired().HasMaxLength(64);

        b.HasIndex(x => new { x.Kind, x.IsActive });
        b.HasIndex(x => new { x.Kind, x.Version }).IsUnique();
    }
}

public sealed class PolicyAcceptanceConfiguration : IEntityTypeConfiguration<PolicyAcceptance>
{
    public void Configure(EntityTypeBuilder<PolicyAcceptance> b)
    {
        b.ToTable("PolicyAcceptances");
        b.HasKey(x => x.Id);

        b.Property(x => x.PolicyKind).HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.PolicyVersion).IsRequired().HasMaxLength(40);
        b.Property(x => x.ContentHash).IsRequired().HasMaxLength(64);
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.UserAgent).HasMaxLength(400);
        b.Property(x => x.SignatureHmac).IsRequired().HasMaxLength(128);

        b.HasIndex(x => x.MemberId);
        b.HasIndex(x => x.PolicyDocumentId);
        b.HasIndex(x => new { x.MemberId, x.PolicyKind, x.AcceptedAtUtc });

        b.HasOne<Member>().WithMany()
            .HasForeignKey(x => x.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne<PolicyDocument>().WithMany()
            .HasForeignKey(x => x.PolicyDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
