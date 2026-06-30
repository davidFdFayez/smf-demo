using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Persistence.Configurations;

internal sealed class DeviceRegistrationConfiguration : IEntityTypeConfiguration<DeviceRegistration>
{
    public void Configure(EntityTypeBuilder<DeviceRegistration> builder)
    {
        builder.ToTable("DeviceRegistrations");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.MemberId).IsRequired();
        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(d => d.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(d => d.Platform)
            .HasConversion<string>().HasMaxLength(16).IsRequired();

        builder.Property(d => d.Token).HasMaxLength(2048).IsRequired();
        builder.Property(d => d.IsActive).IsRequired();
        builder.Property(d => d.RegisteredAtUtc).IsRequired();
        builder.Property(d => d.LastSeenAtUtc).IsRequired();

        builder.HasIndex(d => new { d.MemberId, d.Token }).IsUnique();
        builder.HasIndex(d => new { d.IsActive, d.MemberId });
    }
}
