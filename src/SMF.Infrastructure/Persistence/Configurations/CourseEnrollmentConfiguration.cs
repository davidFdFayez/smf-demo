using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Persistence.Configurations;

internal sealed class CourseEnrollmentConfiguration : IEntityTypeConfiguration<CourseEnrollment>
{
    public void Configure(EntityTypeBuilder<CourseEnrollment> builder)
    {
        builder.ToTable("CourseEnrollments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.CourseId).IsRequired();
        builder.Property(x => x.MemberId).IsRequired();
        builder.Property(x => x.EnrolledAtUtc).IsRequired();
        builder.Property(x => x.CompletedAtUtc);

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).IsRequired();

        // A member is enrolled at most once per course (re-enroll = resurrect the row).
        builder.HasIndex(x => new { x.CourseId, x.MemberId }).IsUnique();

        builder.HasMany(x => x.Completions)
            .WithOne()
            .HasForeignKey(c => c.EnrollmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Completions).AutoInclude(false);
    }
}

internal sealed class LessonCompletionConfiguration : IEntityTypeConfiguration<LessonCompletion>
{
    public void Configure(EntityTypeBuilder<LessonCompletion> builder)
    {
        builder.ToTable("LessonCompletions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.EnrollmentId).IsRequired();
        builder.Property(x => x.LessonId).IsRequired();
        builder.Property(x => x.CompletedAtUtc).IsRequired();

        builder.HasIndex(x => new { x.EnrollmentId, x.LessonId }).IsUnique();
    }
}
