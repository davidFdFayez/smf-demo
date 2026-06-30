using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Persistence.Configurations;

internal sealed class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable("Members");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.FullName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.DateOfBirth)
            .IsRequired();

        builder.Property(x => x.Role)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.SMF_ID)
            .HasColumnName("SmfId")
            .IsRequired()
            .HasMaxLength(32);

        builder.HasIndex(x => x.SMF_ID).IsUnique();

        builder.Property(x => x.RegistrationStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.GuardianConsent)
            .IsRequired();

        // Contact fields. Defaults keep the migration applicable to existing
        // seeded rows from earlier schema versions; domain-level Register()
        // enforces non-empty for all new registrations.
        builder.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(200)
            .HasDefaultValue(string.Empty);

        builder.Property(x => x.PhoneNumber)
            .IsRequired()
            .HasMaxLength(32)
            .HasDefaultValue(string.Empty);

        builder.Property(x => x.NationalId)
            .IsRequired()
            .HasMaxLength(32)
            .HasDefaultValue(string.Empty);

        // Non-unique index on Email is enough here — two members sharing
        // the same email is discouraged by validation but not a domain
        // invariant (e.g. a family using one address for two minors).
        builder.HasIndex(x => x.Email);
        builder.HasIndex(x => x.NationalId);

        // Compliance timestamps. A sentinel default of 1970-01-01 ("Unix
        // epoch") is recognised as "never accepted" by any future reporting
        // job; all new rows land with real UTC stamps from the handler.
        var sentinel = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        builder.Property(x => x.TermsAcceptedAtUtc)
            .IsRequired()
            .HasDefaultValue(sentinel);

        builder.Property(x => x.PrivacyPolicyAcceptedAtUtc)
            .IsRequired()
            .HasDefaultValue(sentinel);

        builder.Property(x => x.CodeOfConductAcceptedAtUtc)
            .IsRequired()
            .HasDefaultValue(sentinel);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        // Role-specific profile fields (PDF §3). Kept nullable at the DB
        // level so the same Members table supports every role; the
        // Application validator enforces conditional required-ness per role.
        builder.Property(x => x.AffiliatedClubId);
        builder.HasIndex(x => x.AffiliatedClubId);

        builder.Property(x => x.LicenseLevel).HasMaxLength(64);
        builder.Property(x => x.YearsOfExperience);

        // Athlete readiness (PDF §5). Nullable — only athletes will populate
        // these. HasColumnType is explicit so EF generates a decimal(5,2)
        // column rather than the SQL Server default of decimal(18,2), which
        // is wasteful for a value physically capped at 3 digits before the
        // decimal point.
        builder.Property(x => x.WeightCategoryKg)
            .HasColumnType("decimal(5,2)");
        builder.Property(x => x.MedicalCleared);
        builder.Property(x => x.MedicalClearedAtUtc);
    }
}
