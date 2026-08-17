using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smart_pet_care_api.Models;

public class ReminderConfiguration : IEntityTypeConfiguration<Reminder>
{
    public void Configure(EntityTypeBuilder<Reminder> builder)
    {
        builder.ToTable("Reminders", t =>
        {
            t.HasCheckConstraint(
                "CK_Reminders_DateRange",
                "\"EndAt\" IS NULL OR \"EndAt\" >= \"StartAt\"");

            t.HasCheckConstraint(
                "CK_Reminders_IntervalN",
                "\"IntervalN\" >= 1");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Title).IsRequired();
        builder.Property(r => r.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(r => r.IntervalN).HasDefaultValue(1);

        builder.HasIndex(r => new { r.PetId, r.NextTriggerAt });
        builder.HasIndex(r => new { r.Status, r.NextTriggerAt });
        builder.HasIndex(r => new { r.SourceType, r.SourceId });

        // Drives the "overdue" surface on Home without scanning runs.
        builder.HasIndex(r => new { r.PetId, r.OverdueSince });

        builder.HasOne(r => r.Pet)
            .WithMany(p => p.Reminders)
            .HasForeignKey(r => r.PetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}