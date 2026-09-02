using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smart_pet_care_api.Models;

public class SleepLogConfiguration : IEntityTypeConfiguration<SleepLog>
{
    public void Configure(EntityTypeBuilder<SleepLog> builder)
    {
        builder.ToTable("SleepLogs", t =>
        {
            t.HasCheckConstraint(
                "CK_SleepLogs_HoursInRange",
                "\"Hours\" > 0 AND \"Hours\" <= 24");
        });

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Hours).HasColumnType("numeric(4,2)");
        builder.Property(s => s.Note).HasMaxLength(2000);
        builder.Property(s => s.CreatedAt).HasDefaultValueSql("now()");

        // Not unique: a day can be logged as several naps. The 24-hour ceiling on the day is
        // a sum across rows, which no constraint can express, so the service holds it.
        builder.HasIndex(s => new { s.PetId, s.SleepDate })
            .IsDescending(false, true);

        builder.HasOne<Pet>()
            .WithMany(p => p.SleepLogs)
            .HasForeignKey(s => s.PetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
