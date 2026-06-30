using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Persistence.Configurations;

internal sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("Courses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Slug).IsRequired().HasMaxLength(220);
        builder.Property(x => x.Summary).IsRequired().HasMaxLength(800);
        builder.Property(x => x.InstructorName).IsRequired().HasMaxLength(120);
        builder.Property(x => x.CoverImageUrl).HasMaxLength(500);

        builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(48).IsRequired();
        builder.Property(x => x.Level).HasConversion<string>().HasMaxLength(24).IsRequired();

        builder.Property(x => x.IsPublished).IsRequired();
        builder.Property(x => x.IsArchived).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.PublishedAtUtc);

        builder.HasIndex(x => x.Slug).IsUnique();
        builder.HasIndex(x => new { x.IsPublished, x.PublishedAtUtc });

        builder.HasMany(x => x.Lessons)
            .WithOne()
            .HasForeignKey(l => l.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Lessons).AutoInclude(false);
    }
}
