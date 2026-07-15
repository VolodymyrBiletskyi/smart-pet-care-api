using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smart_pet_care_api.Models;

namespace smart_pet_care_api.Data.Configurations;

public sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("ChatMessages");
        builder.HasKey(message => message.Id);

        builder.Property(message => message.Content).IsRequired().HasColumnType("text");
        builder.Property(message => message.Status).HasColumnType("integer");
        builder.Property(message => message.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(message => new { message.SessionId, message.CreatedAt });

        builder.HasOne<ChatSession>()
            .WithMany(session => session.Messages)
            .HasForeignKey(message => message.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
