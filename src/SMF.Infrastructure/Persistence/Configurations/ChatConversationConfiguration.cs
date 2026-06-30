using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMF.Domain.Entities;

namespace SMF.Infrastructure.Persistence.Configurations;

internal sealed class ChatConversationConfiguration : IEntityTypeConfiguration<ChatConversation>
{
    public void Configure(EntityTypeBuilder<ChatConversation> builder)
    {
        builder.ToTable("ChatConversations");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.MemberId);
        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(c => c.MemberId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(c => c.GuestKey);
        builder.Property(c => c.Title).HasMaxLength(200).IsRequired();
        builder.Property(c => c.CreatedAtUtc).IsRequired();
        builder.Property(c => c.UpdatedAtUtc).IsRequired();

        builder.HasMany(c => c.Messages)
            .WithOne()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(ChatConversation.Messages))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(c => c.MemberId);
        builder.HasIndex(c => c.GuestKey);
        builder.HasIndex(c => c.UpdatedAtUtc);
    }
}

internal sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("ChatMessages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.ConversationId).IsRequired();
        builder.Property(m => m.Role)
            .HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(m => m.Content).IsRequired();
        builder.Property(m => m.CreatedAtUtc).IsRequired();

        builder.HasIndex(m => new { m.ConversationId, m.CreatedAtUtc });
    }
}
