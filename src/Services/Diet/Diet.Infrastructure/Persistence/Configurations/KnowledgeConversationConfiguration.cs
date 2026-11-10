using Diet.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diet.Infrastructure.Persistence.Configurations;

public sealed class KnowledgeConversationConfiguration : IEntityTypeConfiguration<KnowledgeConversation>
{
    public void Configure(EntityTypeBuilder<KnowledgeConversation> builder)
    {
        builder.ToTable("knowledge_conversations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.UpdatedAt });
        builder.HasMany(x => x.Messages)
            .WithOne()
            .HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Messages).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class KnowledgeMessageConfiguration : IEntityTypeConfiguration<KnowledgeMessage>
{
    public void Configure(EntityTypeBuilder<KnowledgeMessage> builder)
    {
        builder.ToTable("knowledge_messages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Role).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Content).IsRequired();
        builder.Property(x => x.CitationsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.AbstainReason).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.ConversationId, x.CreatedAt });
    }
}
