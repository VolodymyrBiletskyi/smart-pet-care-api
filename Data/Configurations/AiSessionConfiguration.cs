using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smart_pet_care_api.Models;

public class AiSessionConfiguration : IEntityTypeConfiguration<AiSession>
{
    public void Configure(EntityTypeBuilder<AiSession> builder)
    {
        builder.ToTable("AiSessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(s => s.UserId);
        builder.HasIndex(s => new { s.UserId, s.CreatedAt })
            .IsDescending(false, true);

        builder.HasOne<User>()
            .WithMany(u => u.AiSessions)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}