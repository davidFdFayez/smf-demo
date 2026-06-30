using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Persistence.Configurations;

internal sealed class ScoringTenantConfiguration : IEntityTypeConfiguration<ScoringTenant>
{
    public void Configure(EntityTypeBuilder<ScoringTenant> builder)
    {
        builder.ToTable("ScoringTenants");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Code).IsRequired().HasMaxLength(48);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(120);
        builder.Property(x => x.PrimaryColor).IsRequired().HasMaxLength(12);
        builder.Property(x => x.AccentColor).IsRequired().HasMaxLength(12);
        builder.Property(x => x.LogoUrl).HasMaxLength(500);
        builder.Property(x => x.DarkLogoUrl).HasMaxLength(500);
        builder.Property(x => x.ContactEmail).IsRequired().HasMaxLength(200);
        builder.Property(x => x.CustomDomain).HasMaxLength(253);
        builder.Property(x => x.WebsiteUrl).HasMaxLength(500);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        builder.HasIndex(x => x.Code).IsUnique();
        // Unique among non-null values so multiple tenants can leave it blank.
        builder.HasIndex(x => x.CustomDomain)
               .IsUnique()
               .HasFilter("[CustomDomain] IS NOT NULL");
    }
}
