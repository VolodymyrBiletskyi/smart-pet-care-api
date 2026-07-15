using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smart_pet_care_api.Models;

namespace smart_pet_care_api.Data.Configurations;

public sealed class ChatSessionConfiguration : IEntityTypeConfiguration<ChatSession>
{
    public void Configure(EntityTypeBuilder<ChatSession> builder)
    {
        builder.ToTable("ChatSessions");
        builder.HasKey(session => session.Id);

        builder.Property(session => session.SymptomSummary).HasColumnType("text");
        builder.Property(session => session.PetType).IsRequired();
        builder.Property(session => session.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(session => session.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(session => session.UserId);
        builder.HasIndex(session => session.PetId).IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(session => session.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Pet>()
            .WithMany()
            .HasForeignKey(session => session.PetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
