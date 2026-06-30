using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Persistence.Configurations;

internal sealed class FederationEventConfiguration : IEntityTypeConfiguration<FederationEvent>
{
    public void Configure(EntityTypeBuilder<FederationEvent> builder)
    {
        builder.ToTable("Events");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.Property(x => x.Location).IsRequired().HasMaxLength(200);

        builder.Property(x => x.StartsAtUtc).IsRequired();
        builder.Property(x => x.EndsAtUtc).IsRequired();
        builder.Property(x => x.RegistrationOpensAtUtc).IsRequired();
        builder.Property(x => x.RegistrationClosesAtUtc).IsRequired();

        builder.Property(x => x.EntryFeeMinor).IsRequired();
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.Capacity);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc).IsRequired();

        // Live streaming (PDF §9) — optional per-event broadcast.
        builder.Property(x => x.LiveStreamProvider)
            .HasConversion<string>()
            .HasMaxLength(24);
        builder.Property(x => x.LiveStreamUrl).HasMaxLength(500);

        // Owned child collection — registrations live and die with the event.
        builder.HasMany(x => x.Registrations)
            .WithOne()
            .HasForeignKey(r => r.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Registrations).AutoInclude(false);
    }
}
