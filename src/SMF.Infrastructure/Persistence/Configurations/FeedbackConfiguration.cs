using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Persistence.Configurations;

internal sealed class FeedbackConfiguration : IEntityTypeConfiguration<Feedback>
{
    public void Configure(EntityTypeBuilder<Feedback> builder)
    {
        builder.ToTable("Feedbacks");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.SubjectType)
            .HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(f => f.SubjectId);

        builder.Property(f => f.Rating).IsRequired();
        builder.Property(f => f.Comment).HasMaxLength(2000).IsRequired();

        builder.Property(f => f.AuthorMemberId);
        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(f => f.AuthorMemberId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(f => f.AuthorName).HasMaxLength(200).IsRequired();
        builder.Property(f => f.AuthorEmail).HasMaxLength(320);

        builder.Property(f => f.Status)
            .HasConversion<string>().HasMaxLength(16).IsRequired();

        builder.Property(f => f.AdminNotes).HasMaxLength(1000);
        builder.Property(f => f.ModeratedByMemberId);
        builder.Property(f => f.ModeratedAtUtc);

        builder.Property(f => f.CreatedAtUtc).IsRequired();

        builder.HasIndex(f => new { f.SubjectType, f.SubjectId });
        builder.HasIndex(f => f.Status);
        builder.HasIndex(f => f.CreatedAtUtc);
    }
}
