using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smart_pet_care_api.Models;

public class AiMessageConfiguration : IEntityTypeConfiguration<AiMessage>
{
    public void Configure(EntityTypeBuilder<AiMessage> builder)
    {
        builder.ToTable("AiMessages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Content).IsRequired();
        builder.Property(m => m.Metadata).HasColumnType("jsonb");
        builder.Property(m => m.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(m => new { m.SessionId, m.CreatedAt });

        builder.HasOne<AiSession>()
            .WithMany(s => s.Messages)
            .HasForeignKey(m => m.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}