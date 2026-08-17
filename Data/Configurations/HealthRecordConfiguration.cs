using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smart_pet_care_api.Models;

public class HealthRecordConfiguration : IEntityTypeConfiguration<HealthRecord>
{
    public void Configure(EntityTypeBuilder<HealthRecord> builder)
    {
        builder.ToTable("HealthRecords", t =>
        {
            t.HasCheckConstraint(
                "CK_HealthRecords_NextDueAfterPerformed",
                "(\"NextDueAt\" IS NULL OR \"NextDueAt\" >= \"PerformedAt\")");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Title).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Description).HasMaxLength(2000);
        builder.Property(r => r.Dosage).HasMaxLength(200);
        builder.Property(r => r.Provider).HasMaxLength(200);

        builder.Property(r => r.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(r => new { r.PetId, r.PerformedAt })
            .IsDescending(false, true);
        builder.HasIndex(r => new { r.PetId, r.Type });
        builder.HasIndex(r => r.ReminderId);

        builder.HasOne<Pet>()
            .WithMany(p => p.HealthRecords)
            .HasForeignKey(r => r.PetId)
            .OnDelete(DeleteBehavior.Cascade);

        // Deleting a reminder must not delete treatment history, so the link is cut instead.
        builder.HasOne<Reminder>()
            .WithMany()
            .HasForeignKey(r => r.ReminderId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
