using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Persistence.Configurations;

internal sealed class CertificateConfiguration : IEntityTypeConfiguration<Certificate>
{
    public void Configure(EntityTypeBuilder<Certificate> builder)
    {
        builder.ToTable("Certificates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.MemberId).IsRequired();

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.Property(x => x.IssuingAuthority).HasMaxLength(200);

        builder.Property(x => x.VerificationCode).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => x.VerificationCode).IsUnique();

        builder.Property(x => x.IssuedAtUtc).IsRequired();
        builder.Property(x => x.ExpiresAtUtc);
        builder.Property(x => x.IsRevoked).IsRequired();
        builder.Property(x => x.RevokedAtUtc);
        builder.Property(x => x.RevocationReason).HasMaxLength(500);

        builder.HasIndex(x => x.MemberId);
    }
}
