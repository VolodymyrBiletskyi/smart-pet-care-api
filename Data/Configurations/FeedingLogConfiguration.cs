using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smart_pet_care_api.Models;

public class FeedingLogConfiguration : IEntityTypeConfiguration<FeedingLog>
{
    public void Configure(EntityTypeBuilder<FeedingLog> builder)
    {
        builder.ToTable("FeedingLogs", t =>
        {
            t.HasCheckConstraint(
                "CK_FeedingLogs_NonNegative",
                "(\"PortionAmount\" IS NULL OR \"PortionAmount\" >= 0) " +
                "AND (\"ApproxCalories\" IS NULL OR \"ApproxCalories\" >= 0)");
        });

        builder.HasKey(f => f.Id);

        builder.Property(f => f.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(f => new { f.PetId, f.FedAt })
            .IsDescending(false, true);

        builder.HasOne<Pet>()
            .WithMany(p => p.FeedingLogs)
            .HasForeignKey(f => f.PetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}