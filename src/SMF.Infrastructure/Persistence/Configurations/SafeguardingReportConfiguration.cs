using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Persistence.Configurations;

internal sealed class SafeguardingReportConfiguration : IEntityTypeConfiguration<SafeguardingReport>
{
    public void Configure(EntityTypeBuilder<SafeguardingReport> builder)
    {
        builder.ToTable("SafeguardingReports");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.ReferenceCode).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Subject).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(5000);
        builder.Property(x => x.IncidentLocation).HasMaxLength(200);
        builder.Property(x => x.IncidentDate);

        builder.Property(x => x.Category)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.IsAnonymous).IsRequired();
        builder.Property(x => x.ReporterName).HasMaxLength(200);
        builder.Property(x => x.ReporterEmail).HasMaxLength(200);
        builder.Property(x => x.ReporterPhone).HasMaxLength(32);
        builder.Property(x => x.ReviewerNotes).HasMaxLength(4000);

        builder.Property(x => x.SubmittedAtUtc).IsRequired();
        builder.Property(x => x.ResolvedAtUtc);

        builder.HasIndex(x => x.ReferenceCode).IsUnique();
        builder.HasIndex(x => x.Status);
    }
}
