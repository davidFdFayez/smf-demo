using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Persistence.Configurations;

internal sealed class EventRegistrationConfiguration : IEntityTypeConfiguration<EventRegistration>
{
    public void Configure(EntityTypeBuilder<EventRegistration> builder)
    {
        builder.ToTable("EventRegistrations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.EventId).IsRequired();
        builder.Property(x => x.MemberId).IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.CheckedInAtUtc);
        builder.Property(x => x.CancelledAtUtc);
        builder.Property(x => x.PaymentId);

        // One non-cancelled registration per (event, member). Filtered so
        // that a prior cancellation doesn't block a re-registration.
        builder.HasIndex(x => new { x.EventId, x.MemberId });
    }
}
