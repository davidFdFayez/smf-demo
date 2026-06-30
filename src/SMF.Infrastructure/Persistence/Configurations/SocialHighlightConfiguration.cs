using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Persistence.Configurations;

internal sealed class SocialHighlightConfiguration : IEntityTypeConfiguration<SocialHighlight>
{
    public void Configure(EntityTypeBuilder<SocialHighlight> builder)
    {
        builder.ToTable("SocialHighlights");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Platform)
            .HasConversion<string>().HasMaxLength(16).IsRequired();

        builder.Property(h => h.Caption).HasMaxLength(500).IsRequired();
        builder.Property(h => h.ExternalUrl).HasMaxLength(1024).IsRequired();
        builder.Property(h => h.EmbedHtml).HasMaxLength(8000);
        builder.Property(h => h.MediaUrl).HasMaxLength(1024);

        builder.Property(h => h.DisplayOrder).IsRequired();
        builder.Property(h => h.IsPublished).IsRequired();
        builder.Property(h => h.PostedAtUtc);
        builder.Property(h => h.CreatedAtUtc).IsRequired();
        builder.Property(h => h.UpdatedAtUtc);

        builder.HasIndex(h => new { h.IsPublished, h.DisplayOrder });
    }
}
