using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Persistence.Configurations;

public sealed class GovernanceDocumentConfiguration : IEntityTypeConfiguration<GovernanceDocument>
{
    public void Configure(EntityTypeBuilder<GovernanceDocument> b)
    {
        b.ToTable("GovernanceDocuments");
        b.HasKey(x => x.Id);

        b.Property(x => x.Title).IsRequired().HasMaxLength(200);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.DocumentType).HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.StorageKey).IsRequired().HasMaxLength(400);
        b.Property(x => x.OriginalFileName).IsRequired().HasMaxLength(260);
        b.Property(x => x.ContentType).IsRequired().HasMaxLength(120);
        b.Property(x => x.Sha256).IsRequired().HasMaxLength(64);

        b.HasIndex(x => x.DocumentType);
        b.HasIndex(x => x.IsPublished);
        b.HasIndex(x => new { x.IsPublished, x.CoveringYear });
    }
}
