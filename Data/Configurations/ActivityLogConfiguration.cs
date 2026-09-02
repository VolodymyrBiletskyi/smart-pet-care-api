using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smart_pet_care_api.Models;

public class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> builder)
    {
        builder.ToTable("ActivityLogs", t =>
        {
            t.HasCheckConstraint(
                "CK_ActivityLogs_StepsNonNegative",
                "\"Steps\" IS NULL OR \"Steps\" >= 0");

            t.HasCheckConstraint(
                "CK_ActivityLogs_DurationMinutesInRange",
                "\"DurationMinutes\" IS NULL OR (\"DurationMinutes\" > 0 AND \"DurationMinutes\" <= 1440)");
        });

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Location).HasMaxLength(200);
        builder.Property(a => a.Note).HasMaxLength(2000);
        builder.Property(a => a.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(a => new { a.PetId, a.RecordedAt })
            .IsDescending(false, true);

        builder.HasOne<Pet>()
            .WithMany(p => p.ActivityLogs)
            .HasForeignKey(a => a.PetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
