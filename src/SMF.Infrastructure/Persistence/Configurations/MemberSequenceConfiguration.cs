using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMF.Infrastructure.Persistence.Entities;

namespace SMF.Infrastructure.Persistence.Configurations;

internal sealed class MemberSequenceConfiguration : IEntityTypeConfiguration<MemberSequence>
{
    public void Configure(EntityTypeBuilder<MemberSequence> builder)
    {
        builder.ToTable("MemberSequences");

        builder.HasKey(x => x.Year);

        builder.Property(x => x.Year)
            .ValueGeneratedNever();

        builder.Property(x => x.LastSequence)
            .IsRequired();

        builder.Property(x => x.RowVersion)
            .IsRowVersion();
    }
}
