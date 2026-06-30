using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Persistence.Configurations;

internal sealed class ClubConfiguration : IEntityTypeConfiguration<Club>
{
    public void Configure(EntityTypeBuilder<Club> builder)
    {
        builder.ToTable("Clubs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Slug).IsRequired().HasMaxLength(220);
        builder.HasIndex(x => x.Slug).IsUnique();

        builder.Property(x => x.City).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ContactEmail).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ContactPhone).IsRequired().HasMaxLength(32);
        builder.Property(x => x.WebsiteUrl).HasMaxLength(500);
        builder.Property(x => x.LogoUrl).HasMaxLength(500);
        builder.Property(x => x.Description).HasMaxLength(2000);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc).IsRequired();

        // Microsite (PDF §9) — all nullable, filled in by club admin UI.
        builder.Property(x => x.MicrositeHeadline).HasMaxLength(200);
        builder.Property(x => x.MicrositeAbout).HasMaxLength(4000);
        builder.Property(x => x.MicrositeHeroImageUrl).HasMaxLength(500);
        builder.Property(x => x.MicrositePrimaryColor).HasMaxLength(12);
        builder.Property(x => x.MicrositeInstagramHandle).HasMaxLength(64);
        builder.Property(x => x.MicrositeTwitterHandle).HasMaxLength(64);
        builder.Property(x => x.MicrositeYoutubeChannel).HasMaxLength(120);
    }
}
